using System.Collections.Concurrent;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NLog;

namespace WaveDude.Runs;

/// <summary>
/// The frames of one run: per frame one <see cref="Half"/> plane per layer (<see cref="Layers"/>),
/// <see cref="Width"/> × <see cref="Height"/> values row-major with the engine's padding stripped. Two tiers
/// (2026-10-14): the first frames of the run, as many as the memory budget given at creation holds
/// (<see cref="RamFrames"/>), live in native memory; the rest go to a temporary file that is created only when
/// a run does not fit. A run that fits never touches the disk, so it is bound by the engine and not by the
/// drive; a run that does not fit is disk-bound for its tail only. The file is pre-sized for the frames it
/// will hold, marked temporary, and deleted when the store is disposed or the process ends
/// (<see cref="FileOptions.DeleteOnClose"/>).
/// </summary>
/// <remarks>
/// Why not the operating system's cache or a memory mapping: both keep dirty pages in memory only up to
/// the memory manager's own allowance and then write at disk speed whatever the file's attributes say
/// (a 10 GB full-HD run took 14 s against the drive with 40 GB of memory free), and a mapping paid a page
/// fault per fresh page and flushed everything on unmap. The budget is ours, decided from what is free when
/// the run starts (<see cref="FrameMemory.Budget"/>), so the machine is never pushed into its page file.
/// Threads: the run's worker is the only writer (<see cref="Write(int, ReadOnlySpan{Half}, ReadOnlySpan{Half})"/>,
/// frames in order); any thread may read frames below <see cref="Count"/>, which is published after the
/// frame's bytes. Reads and writes run concurrently; <see cref="Dispose"/> waits for them, frees the memory
/// under the lock (quick) and closes the file outside it (deleting a large file can take a second).
/// A background task allocates the memory blocks a few frames ahead of the writer and touches every page of
/// them (<see cref="PrepareBlocks"/>): a fresh block costs a page fault per 4 KB on first touch, 8 to 14 ms
/// for a full-HD frame, which would make the writer slower than the engine; prepared, a frame is a plain copy.
/// File layout: a <see cref="HeaderSize"/>-byte header ("WDFS", version, width, height, layers,
/// visualize-every, capacity, first frame in the file), then one <see cref="FrameBytes"/> record per spilled
/// frame; within a frame the planes follow the order of <see cref="FrameLayer"/>, only those present.
/// </remarks>
public sealed unsafe class FrameStore : IFrameSource, IDisposable
{
	private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

	/// <summary>Size of the file header in bytes.</summary>
	public const int HeaderSize = 64;

	private const uint Magic = 0x53464457; // "WDFS" as little-endian bytes
	private const int Version = 2;

	/// <summary>Value of <see cref="_firstSpilled"/> while no frame goes to the file.</summary>
	private const int NoSpill = int.MaxValue;

	/// <summary>Memory blocks allocated and touched ahead of the writer.</summary>
	private const int PrepareDepth = 4;

	/// <summary>Page size assumed when touching a fresh block.</summary>
	private const int PageSize = 4096;

	private readonly ReaderWriterLockSlim _lifetime = new();
	private readonly IntPtr[] _ram;
	private readonly BlockingCollection<IntPtr> _prepared = new(PrepareDepth);
	private readonly CancellationTokenSource _prepareCancel = new();
	private readonly Task _preparer;
	private readonly Half[] _staging;
	private Half[]? _staging2;
	private FileStream? _file;
	private SafeFileHandle? _handle;
	private volatile int _firstSpilled = NoSpill;
	private int _count;
	private bool _disposed;

	private FrameStore(string path, int width, int height, FrameLayers layers, int capacity, int visualizeEvery, long ramBudget)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(visualizeEvery, 1);
		if (layers == FrameLayers.None)
		{
			throw new ArgumentException("A store needs at least one layer.", nameof(layers));
		}
		Path = path;
		Width = width;
		Height = height;
		Layers = layers;
		Capacity = capacity;
		VisualizeEvery = visualizeEvery;
		LayerCount = (Has(FrameLayer.Field) ? 1 : 0) + (Has(FrameLayer.Intensity) ? 1 : 0);
		RamFrames = Math.Min(capacity, FramesInBudget(width, height, layers, ramBudget));
		_ram = new IntPtr[RamFrames];
		_staging = new Half[width * height];
		if (RamFrames < capacity)
		{
			// the run will not fit: the file for the rest is made now, so a lack of disk space shows at the start
			_firstSpilled = RamFrames;
			CreateFile(RamFrames);
		}
		_preparer = RamFrames > 0 ? Task.Run(PrepareBlocks) : Task.CompletedTask;
	}

	/// <summary>
	/// The background producer of memory blocks: allocates one per frame of the memory tier, touches every page
	/// so the writer never takes a page fault, and queues it, at most <see cref="PrepareDepth"/> ahead. Ends when
	/// every block is made, the store is disposed, or an allocation fails (the commit limit), after which the
	/// writer spills the remaining frames to the file.
	/// </summary>
	private void PrepareBlocks()
	{
		CancellationToken token = _prepareCancel.Token;
		try
		{
			for (int i = 0; i < RamFrames && !token.IsCancellationRequested; i++)
			{
				void* block;
				try
				{
					block = NativeMemory.Alloc((nuint)FrameBytes);
				}
				catch (OutOfMemoryException)
				{
					break;
				}
				byte* bytes = (byte*)block;
				for (long offset = 0; offset < FrameBytes; offset += PageSize)
				{
					bytes[offset] = 0;
				}
				try
				{
					_prepared.Add((IntPtr)block, token);
				}
				catch (OperationCanceledException)
				{
					NativeMemory.Free(block);
					break;
				}
			}
		}
		finally
		{
			_prepared.CompleteAdding();
		}
	}

	/// <summary>The next prepared block for a frame of the memory tier, waiting for the producer; false when it ended (the memory is exhausted).</summary>
	private bool TryTakeBlock(out void* block)
	{
		if (_prepared.TryTake(out IntPtr pointer, Timeout.Infinite))
		{
			block = (void*)pointer;
			return true;
		}
		block = null;
		return false;
	}

	/// <summary>
	/// Creates a store for a run of <paramref name="capacity"/> frames: in memory up to <paramref name="ramBudget"/>
	/// bytes, the rest in the file at <paramref name="path"/> (which must not exist; it is only created when needed).
	/// </summary>
	/// <param name="path">File path for the spilled frames, normally from <see cref="FrameCache.NewPath"/>.</param>
	/// <param name="width">Frame width in pixels.</param>
	/// <param name="height">Frame height in pixels.</param>
	/// <param name="layers">The planes every frame holds; at least one.</param>
	/// <param name="capacity">Number of frames the run produces (<see cref="RunSpec.FrameCount"/>).</param>
	/// <param name="visualizeEvery">Steps per frame, kept for <see cref="StepOf"/>.</param>
	/// <param name="ramBudget">Bytes of memory the frames may take; null takes <see cref="FrameMemory.Budget"/> at the default share.</param>
	/// <exception cref="IOException">The file cannot be created or sized (for instance no disk space).</exception>
	public static FrameStore Create(string path, int width, int height, FrameLayers layers, int capacity, int visualizeEvery, long? ramBudget = null) =>
		new(path, width, height, layers, capacity, visualizeEvery, ramBudget ?? FrameMemory.Budget(FrameMemory.DefaultPercent));

	/// <summary>Bytes a file holding <paramref name="capacity"/> frames of these dimensions takes, for <see cref="FrameCache.CheckSpace"/> (the spilled part of a run).</summary>
	public static long SizeOf(int width, int height, FrameLayers layers, int capacity) => HeaderSize + (long)capacity * FrameBytesOf(width, height, layers);

	/// <summary>How many frames of these dimensions fit into <paramref name="budget"/> bytes of memory.</summary>
	public static int FramesInBudget(int width, int height, FrameLayers layers, long budget) =>
		(int)Math.Clamp(Math.Max(0, budget) / FrameBytesOf(width, height, layers), 0, int.MaxValue);

	private static long FrameBytesOf(int width, int height, FrameLayers layers)
	{
		int layerCount = ((layers & FrameLayers.Field) != 0 ? 1 : 0) + ((layers & FrameLayers.Intensity) != 0 ? 1 : 0);
		return (long)layerCount * width * height * 2;
	}

	/// <summary>Full path of the file for the spilled frames; it exists only once a frame went there (<see cref="HasFile"/>).</summary>
	public string Path { get; }

	/// <summary>Frame width in pixels.</summary>
	public int Width { get; }

	/// <summary>Frame height in pixels.</summary>
	public int Height { get; }

	/// <summary>The planes every frame holds.</summary>
	public FrameLayers Layers { get; }

	/// <summary>Number of planes per frame.</summary>
	public int LayerCount { get; }

	/// <summary>Number of frames the store has room for.</summary>
	public int Capacity { get; }

	/// <summary>Steps per frame: frame k shows the state after step k × this.</summary>
	public int VisualizeEvery { get; }

	/// <summary>Frames the memory budget holds, the first ones of the run; the rest go to the file.</summary>
	public int RamFrames { get; }

	/// <summary>Index of the first frame kept in the file; <see cref="Capacity"/> when every frame fits in memory. Falls below <see cref="RamFrames"/> only when an allocation failed.</summary>
	public int FirstSpilledFrame => Math.Min(_firstSpilled, Capacity);

	/// <summary>Frames written so far; frames 0..Count-1 may be read from any thread.</summary>
	public int Count => Volatile.Read(ref _count);

	/// <summary>Frames written so far that live in memory.</summary>
	public int FramesInMemory => Math.Min(Count, FirstSpilledFrame);

	/// <summary>Frames written so far that live in the file.</summary>
	public int FramesOnDisk => Math.Max(0, Count - FirstSpilledFrame);

	/// <summary>Whether the file for spilled frames exists.</summary>
	public bool HasFile => _file != null;

	/// <summary>Bytes of one plane: <see cref="Width"/> × <see cref="Height"/> × 2.</summary>
	public long PlaneBytes => (long)Width * Height * 2;

	/// <summary>Bytes of one frame: <see cref="PlaneBytes"/> × <see cref="LayerCount"/>.</summary>
	public long FrameBytes => PlaneBytes * LayerCount;

	/// <summary>Bytes all frames of the run take once written, memory and file together (without the file header).</summary>
	public long TotalBytes => Capacity * FrameBytes;

	/// <summary>Bytes the memory tier takes once its frames are written.</summary>
	public long RamBytes => FirstSpilledFrame * FrameBytes;

	/// <summary>Size of the file, 0 while there is none.</summary>
	public long FileBytes => _file == null ? 0 : HeaderSize + (Capacity - FirstSpilledFrame) * FrameBytes;

	/// <summary>Whether frames hold the given plane.</summary>
	public bool Has(FrameLayer layer) => (Layers & (FrameLayers)(1 << (int)layer)) != 0;

	/// <summary>The 0-based simulation step frame <paramref name="frame"/> shows.</summary>
	public int StepOf(int frame) => frame * VisualizeEvery;

	/// <summary>Raised on the writer's thread after <see cref="Count"/> advanced, with the new count.</summary>
	public event Action<int>? FrameWritten;

	/// <summary>
	/// Stores the next frame from the engine's padded buffers (row y starts at <c>(y + 1) × stride + 1</c>),
	/// converting to <see cref="Half"/> in parallel over the rows first. Writer thread only; frames must arrive
	/// in order. The run itself uses the <see cref="Half"/> overload with the engine's own captures.
	/// </summary>
	/// <param name="frame">Index of the frame; must equal <see cref="Count"/>.</param>
	/// <param name="field">Padded field buffer, <c>stride × (Height + 2)</c> floats.</param>
	/// <param name="intensity">Padded intensity buffer, or null when the store has no intensity plane.</param>
	/// <param name="stride">Row pitch of the padded buffers (<see cref="Width"/> + 2).</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="frame"/> is not the next frame or beyond <see cref="Capacity"/>.</exception>
	/// <exception cref="ArgumentNullException">The store has an intensity plane but <paramref name="intensity"/> is null.</exception>
	/// <exception cref="ObjectDisposedException">The store is disposed.</exception>
	public void Write(int frame, float[] field, float[]? intensity, int stride)
	{
		int width = Width;
		Half[] fieldPlane = _staging;
		Parallel.For(0, Height, y => TensorPrimitives.ConvertToHalf(field.AsSpan((y + 1) * stride + 1, width), fieldPlane.AsSpan(y * width, width)));
		Half[]? intensityPlane = null;
		if (Has(FrameLayer.Intensity))
		{
			ArgumentNullException.ThrowIfNull(intensity);
			intensityPlane = _staging2 ??= new Half[Width * Height];
			Parallel.For(0, Height, y => TensorPrimitives.ConvertToHalf(intensity.AsSpan((y + 1) * stride + 1, width), intensityPlane.AsSpan(y * width, width)));
		}
		Write(frame, fieldPlane, intensityPlane);
	}

	/// <summary>
	/// Stores the next frame from <see cref="Half"/> planes in the store's own layout (what
	/// <see cref="Engine.IWaveEngine.CaptureFrame"/> produces): a copy into memory while the budget lasts, one
	/// positional write per plane into the file beyond it. Any thread, but only one writer at a time; frames
	/// must arrive in order. When the memory for a frame within the budget cannot be had after all (the
	/// commit limit of the machine), that frame and every later one go to the file instead, noted once.
	/// </summary>
	/// <param name="frame">Index of the frame; must equal <see cref="Count"/>.</param>
	/// <param name="field">Field plane, <see cref="Width"/> × <see cref="Height"/> values.</param>
	/// <param name="intensity">Intensity plane, or null when the store has no intensity plane.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="frame"/> is not the next frame or beyond <see cref="Capacity"/>, or a plane is too short.</exception>
	/// <exception cref="ArgumentNullException">The store has an intensity plane but <paramref name="intensity"/> is null.</exception>
	/// <exception cref="ObjectDisposedException">The store is disposed.</exception>
	public void Write(int frame, ReadOnlySpan<Half> field, ReadOnlySpan<Half> intensity)
	{
		int length = Width * Height;
		_lifetime.EnterReadLock();
		try
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (frame != _count || frame >= Capacity)
			{
				throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frames must be written in order and within the capacity.");
			}
			if (Has(FrameLayer.Field) && field.Length < length)
			{
				throw new ArgumentOutOfRangeException(nameof(field), "The field plane is too short.");
			}
			if (Has(FrameLayer.Intensity) && intensity.Length < length)
			{
				throw new ArgumentNullException(nameof(intensity), "The store has an intensity plane; the intensity is missing or too short.");
			}
			if (frame < _firstSpilled)
			{
				if (!TryTakeBlock(out void* memory))
				{
					Spill(frame);
				}
				else
				{
					if (Has(FrameLayer.Field))
					{
						field[..length].CopyTo(Plane(memory, FrameLayer.Field));
					}
					if (Has(FrameLayer.Intensity))
					{
						intensity[..length].CopyTo(Plane(memory, FrameLayer.Intensity));
					}
					_ram[frame] = (IntPtr)memory;
				}
			}
			if (frame >= _firstSpilled)
			{
				if (Has(FrameLayer.Field))
				{
					RandomAccess.Write(_handle!, MemoryMarshal.AsBytes(field[..length]), PlaneOffset(frame, FrameLayer.Field));
				}
				if (Has(FrameLayer.Intensity))
				{
					RandomAccess.Write(_handle!, MemoryMarshal.AsBytes(intensity[..length]), PlaneOffset(frame, FrameLayer.Intensity));
				}
			}
			Volatile.Write(ref _count, frame + 1);
		}
		finally
		{
			_lifetime.ExitReadLock();
		}
		FrameWritten?.Invoke(frame + 1);
	}

	/// <summary>Stores a captured frame (<see cref="Engine.IWaveEngine.CaptureFrame"/>, awaited); the snapshot's size must match.</summary>
	/// <exception cref="ArgumentException">The snapshot has another size.</exception>
	public void Write(int frame, Engine.FrameSnapshot snapshot)
	{
		if (snapshot.Width != Width || snapshot.Height != Height)
		{
			throw new ArgumentException("The snapshot does not match the store's size.", nameof(snapshot));
		}
		Write(frame, snapshot.Field, snapshot.HasIntensity ? snapshot.Intensity : null);
	}

	/// <summary>
	/// Reads one plane of a stored frame into <paramref name="destination"/> (at least <see cref="Width"/> ×
	/// <see cref="Height"/> values), from memory or from the file. Safe against a concurrent <see cref="Dispose"/>;
	/// returns false instead of reading when the store is already disposed.
	/// </summary>
	/// <param name="frame">Frame index below <see cref="Count"/>.</param>
	/// <param name="layer">A plane the store holds.</param>
	/// <param name="destination">Receives the values row-major.</param>
	/// <exception cref="ArgumentOutOfRangeException">The frame is not written yet, the plane is not stored, or the destination is too small.</exception>
	public bool CopyLayer(int frame, FrameLayer layer, Span<Half> destination)
	{
		int length = Width * Height;
		if (destination.Length < length)
		{
			throw new ArgumentOutOfRangeException(nameof(destination), "The destination is smaller than a plane.");
		}
		_lifetime.EnterReadLock();
		try
		{
			if (_disposed)
			{
				return false;
			}
			if (frame < 0 || frame >= Count)
			{
				throw new ArgumentOutOfRangeException(nameof(frame), frame, "The frame is not written yet.");
			}
			if (!Has(layer))
			{
				throw new ArgumentOutOfRangeException(nameof(layer), layer, "The store has no such plane.");
			}
			if (frame < _firstSpilled)
			{
				Plane((void*)_ram[frame], layer).CopyTo(destination);
				return true;
			}
			Span<byte> bytes = MemoryMarshal.AsBytes(destination[..length]);
			long offset = PlaneOffset(frame, layer);
			while (bytes.Length > 0)
			{
				int read = RandomAccess.Read(_handle!, bytes, offset);
				if (read <= 0)
				{
					throw new IOException("The frame store file ended early.");
				}
				bytes = bytes[read..];
				offset += read;
			}
			return true;
		}
		catch (ObjectDisposedException)
		{
			// disposed while reading: the frame is gone with the run
			return false;
		}
		finally
		{
			_lifetime.ExitReadLock();
		}
	}

	/// <summary>
	/// Frees the memory tier and closes the file, which deletes it. Marks the store disposed and frees the frames
	/// under the lock (so no read or write starts on freed memory) but closes the file outside it: deleting a
	/// large file can take a second, and a reader must not wait for that. A file read or write in flight ends
	/// with <see cref="ObjectDisposedException"/>, which <see cref="CopyLayer"/> turns into false.
	/// </summary>
	public void Dispose()
	{
		_lifetime.EnterWriteLock();
		try
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			_prepareCancel.Cancel();
			for (int i = 0; i < _ram.Length; i++)
			{
				if (_ram[i] != IntPtr.Zero)
				{
					NativeMemory.Free((void*)_ram[i]);
					_ram[i] = IntPtr.Zero;
				}
			}
		}
		finally
		{
			_lifetime.ExitWriteLock();
		}
		_file?.Dispose();
		// the producer ends at its next check; the blocks it queued (and any it adds before noticing) are freed here
		try
		{
			_preparer.Wait(TimeSpan.FromSeconds(5));
		}
		catch (AggregateException)
		{
			// cancelled
		}
		while (_prepared.TryTake(out IntPtr block))
		{
			NativeMemory.Free((void*)block);
		}
		_prepareCancel.Dispose();
	}

	/// <summary>A plane of a frame kept in memory, as a span over the native block.</summary>
	private Span<Half> Plane(void* frameMemory, FrameLayer layer) =>
		new((byte*)frameMemory + PlaneIndex(layer) * PlaneBytes, Width * Height);

	/// <summary>Byte offset of a plane in the file; the file holds the frames from <see cref="_firstSpilled"/> on.</summary>
	private long PlaneOffset(int frame, FrameLayer layer) =>
		HeaderSize + (frame - _firstSpilled) * FrameBytes + PlaneIndex(layer) * PlaneBytes;

	/// <summary>Position of a plane within a frame: planes follow the <see cref="FrameLayer"/> order, only those present.</summary>
	private int PlaneIndex(FrameLayer layer) => layer == FrameLayer.Intensity && Has(FrameLayer.Field) ? 1 : 0;

	/// <summary>
	/// The memory for frame <paramref name="frame"/> could not be had: from here on every frame goes to the file,
	/// which is created now if the run was expected to fit, or enlarged to take the extra frames otherwise
	/// (nothing has been written to it yet, since frames arrive in order).
	/// </summary>
	private void Spill(int frame)
	{
		Logger.Warn("No memory for frame {Frame} ({Bytes} per frame): the remaining {Rest} frames go to disk", frame, FrameBytes, Capacity - frame);
		_firstSpilled = frame;
		if (_file == null)
		{
			CreateFile(frame);
		}
		else
		{
			_file.SetLength(HeaderSize + (Capacity - frame) * FrameBytes);
			WriteHeader(frame);
		}
	}

	/// <summary>Creates the pre-sized temporary file for the frames from <paramref name="firstFrame"/> on.</summary>
	private void CreateFile(int firstFrame)
	{
		_file = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
		try
		{
			// a temporary file: the cache manager keeps its pages in memory while it can instead of writing
			// them out eagerly (a hint only); the file is deleted in the end
			try
			{
				File.SetAttributes(Path, FileAttributes.Temporary | FileAttributes.NotContentIndexed);
			}
			catch (IOException)
			{
				// a hint only
			}
			catch (UnauthorizedAccessException)
			{
				// a hint only
			}
			_file.SetLength(HeaderSize + (Capacity - firstFrame) * FrameBytes);
			_handle = _file.SafeFileHandle;
			WriteHeader(firstFrame);
		}
		catch
		{
			_file.Dispose();
			_file = null;
			_handle = null;
			throw;
		}
	}

	private void WriteHeader(int firstFrame)
	{
		Span<byte> header = stackalloc byte[HeaderSize];
		BitConverter.TryWriteBytes(header, Magic);
		BitConverter.TryWriteBytes(header[4..], Version);
		BitConverter.TryWriteBytes(header[8..], Width);
		BitConverter.TryWriteBytes(header[12..], Height);
		BitConverter.TryWriteBytes(header[16..], (int)Layers);
		BitConverter.TryWriteBytes(header[20..], VisualizeEvery);
		BitConverter.TryWriteBytes(header[24..], Capacity);
		BitConverter.TryWriteBytes(header[28..], firstFrame);
		RandomAccess.Write(_handle!, header, 0);
	}
}
