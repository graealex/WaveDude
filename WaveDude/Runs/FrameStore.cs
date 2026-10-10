using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WaveDude.Runs;

/// <summary>
/// The frames of one run: a file holding, per frame, one <see cref="Half"/> plane per layer
/// (<see cref="Layers"/>), <see cref="Width"/> × <see cref="Height"/> values row-major with the engine's
/// padding stripped. The file is pre-sized for <see cref="Capacity"/> frames and marked temporary, so the
/// operating system's cache keeps recent frames in memory and spills the rest to disk; it is deleted when
/// the store is disposed (or the process ends, <see cref="FileOptions.DeleteOnClose"/>).
/// </summary>
/// <remarks>
/// Threads: the run's worker is the only writer (<see cref="Write(int, ReadOnlySpan{Half}, ReadOnlySpan{Half})"/>, frames in order); any thread may read
/// frames below <see cref="Count"/>, which is published after the frame's bytes. Reads and writes are
/// positional (<see cref="RandomAccess"/>) and run concurrently; <see cref="Dispose"/> waits for them.
/// Access goes through the cache manager rather than a memory mapping on purpose: touching 16 MB of fresh
/// mapped pages per frame cost more than the frame's conversion, on both the writer and the reader side,
/// and unmapping flushed the whole file to disk before its deletion.
/// Layout: a <see cref="HeaderSize"/>-byte header ("WDFS", version, width, height, layers, visualize-every,
/// capacity), then <see cref="Capacity"/> × <see cref="FrameBytes"/>; within a frame the planes follow the
/// order of <see cref="FrameLayer"/>, only those present.
/// </remarks>
public sealed class FrameStore : IFrameSource, IDisposable
{
	/// <summary>Size of the file header in bytes.</summary>
	public const int HeaderSize = 64;

	private const uint Magic = 0x53464457; // "WDFS" as little-endian bytes
	private const int Version = 1;

	private readonly ReaderWriterLockSlim _lifetime = new();
	private readonly FileStream _file;
	private readonly SafeFileHandle _handle;
	private readonly Half[] _staging;
	private Half[]? _staging2;
	private int _count;
	private bool _disposed;

	private FrameStore(string path, int width, int height, FrameLayers layers, int capacity, int visualizeEvery)
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
		_staging = new Half[width * height];
		_file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
		try
		{
			// a temporary file: the cache manager keeps its pages in memory while it can instead of writing
			// them out eagerly; the data is read back from the cache and the file is deleted in the end
			try
			{
				File.SetAttributes(path, FileAttributes.Temporary | FileAttributes.NotContentIndexed);
			}
			catch (IOException)
			{
				// a hint only
			}
			catch (UnauthorizedAccessException)
			{
				// a hint only
			}
			_file.SetLength(TotalBytes);
			_handle = _file.SafeFileHandle;
			Span<byte> header = stackalloc byte[HeaderSize];
			BitConverter.TryWriteBytes(header, Magic);
			BitConverter.TryWriteBytes(header[4..], Version);
			BitConverter.TryWriteBytes(header[8..], width);
			BitConverter.TryWriteBytes(header[12..], height);
			BitConverter.TryWriteBytes(header[16..], (int)layers);
			BitConverter.TryWriteBytes(header[20..], visualizeEvery);
			BitConverter.TryWriteBytes(header[24..], capacity);
			RandomAccess.Write(_handle, header, 0);
		}
		catch
		{
			_file.Dispose();
			throw;
		}
	}

	/// <summary>Creates the file at <paramref name="path"/> (which must not exist) sized for the whole run.</summary>
	/// <param name="path">File path, normally from <see cref="FrameCache.NewPath"/>.</param>
	/// <param name="width">Frame width in pixels.</param>
	/// <param name="height">Frame height in pixels.</param>
	/// <param name="layers">The planes every frame holds; at least one.</param>
	/// <param name="capacity">Number of frames the run produces (<see cref="RunSpec.FrameCount"/>).</param>
	/// <param name="visualizeEvery">Steps per frame, kept for <see cref="StepOf"/>.</param>
	/// <exception cref="IOException">The file cannot be created or sized (for instance no disk space).</exception>
	public static FrameStore Create(string path, int width, int height, FrameLayers layers, int capacity, int visualizeEvery) =>
		new(path, width, height, layers, capacity, visualizeEvery);

	/// <summary>Bytes the file of a store with these dimensions takes, for <see cref="FrameCache.CheckSpace"/>.</summary>
	public static long SizeOf(int width, int height, FrameLayers layers, int capacity)
	{
		int layerCount = ((layers & FrameLayers.Field) != 0 ? 1 : 0) + ((layers & FrameLayers.Intensity) != 0 ? 1 : 0);
		return HeaderSize + (long)capacity * layerCount * width * height * 2;
	}

	/// <summary>Full path of the backing file.</summary>
	public string Path { get; }

	/// <summary>Frame width in pixels.</summary>
	public int Width { get; }

	/// <summary>Frame height in pixels.</summary>
	public int Height { get; }

	/// <summary>The planes every frame holds.</summary>
	public FrameLayers Layers { get; }

	/// <summary>Number of planes per frame.</summary>
	public int LayerCount { get; }

	/// <summary>Number of frames the file has room for.</summary>
	public int Capacity { get; }

	/// <summary>Steps per frame: frame k shows the state after step k × this.</summary>
	public int VisualizeEvery { get; }

	/// <summary>Frames written so far; frames 0..Count-1 may be read from any thread.</summary>
	public int Count => Volatile.Read(ref _count);

	/// <summary>Bytes of one plane: <see cref="Width"/> × <see cref="Height"/> × 2.</summary>
	public long PlaneBytes => (long)Width * Height * 2;

	/// <summary>Bytes of one frame: <see cref="PlaneBytes"/> × <see cref="LayerCount"/>.</summary>
	public long FrameBytes => PlaneBytes * LayerCount;

	/// <summary>Size of the whole file.</summary>
	public long TotalBytes => HeaderSize + Capacity * FrameBytes;

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
	/// <see cref="Engine.IWaveEngine.CaptureFrame"/> produces), one positional write per plane. Any thread, but
	/// only one writer at a time; frames must arrive in order.
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
			if (Has(FrameLayer.Field))
			{
				if (field.Length < length)
				{
					throw new ArgumentOutOfRangeException(nameof(field), "The field plane is too short.");
				}
				RandomAccess.Write(_handle, MemoryMarshal.AsBytes(field[..length]), PlaneOffset(frame, FrameLayer.Field));
			}
			if (Has(FrameLayer.Intensity))
			{
				if (intensity.Length < length)
				{
					throw new ArgumentNullException(nameof(intensity), "The store has an intensity plane; the intensity is missing or too short.");
				}
				RandomAccess.Write(_handle, MemoryMarshal.AsBytes(intensity[..length]), PlaneOffset(frame, FrameLayer.Intensity));
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
	/// <see cref="Height"/> values). Safe against a concurrent <see cref="Dispose"/>; returns false instead of
	/// reading when the store is already disposed.
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
			Span<byte> bytes = MemoryMarshal.AsBytes(destination[..length]);
			long offset = PlaneOffset(frame, layer);
			while (bytes.Length > 0)
			{
				int read = RandomAccess.Read(_handle, bytes, offset);
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
	/// Closes the file, which deletes it. Marks the store disposed under the lock (so no new read or write
	/// starts) but closes the file outside it: deleting a 10 GB file can take a second, and a reader must not
	/// wait for that. A read or write in flight ends with <see cref="ObjectDisposedException"/>, which
	/// <see cref="CopyLayer"/> turns into false.
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
		}
		finally
		{
			_lifetime.ExitWriteLock();
		}
		_file.Dispose();
	}

	/// <summary>Byte offset of a plane; planes follow the <see cref="FrameLayer"/> order, only those present.</summary>
	private long PlaneOffset(int frame, FrameLayer layer)
	{
		int planeIndex = layer == FrameLayer.Intensity && Has(FrameLayer.Field) ? 1 : 0;
		return HeaderSize + frame * FrameBytes + planeIndex * PlaneBytes;
	}

}
