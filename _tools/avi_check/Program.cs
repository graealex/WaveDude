using System;
using System.IO;
using System.Text;
using _2D_wave_sim_V01;

// Writes a 3-frame MJPEG AVI with dummy payloads, then walks the RIFF tree and checks every
// size/offset against what the AVI spec expects. Exit code 0 = structure is valid.
internal static class Program
{
	private static int _checks;
	private static int _failures;

	private static void Check(bool ok, string what)
	{
		Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
		_checks++;
		if (!ok) _failures++;
	}

	private static int Main()
	{
		string path = Path.Combine(Path.GetTempPath(), "avi_check.avi");
		int[] frameSizes = { 1001, 2000, 777 }; // odd sizes exercise the pad byte
		using (var w = new AviMjpegWriter(path, 320, 240, 25))
		{
			foreach (int n in frameSizes)
			{
				var payload = new byte[n];
				for (int i = 0; i < n; i++) payload[i] = (byte)i;
				w.AddFrame(payload, n);
			}
		}
		byte[] f = File.ReadAllBytes(path);
		Func<int, string> fourcc = o => Encoding.ASCII.GetString(f, o, 4);
		Func<int, uint> u32 = o => BitConverter.ToUInt32(f, o);
		Func<int, ushort> u16 = o => BitConverter.ToUInt16(f, o);

		Console.WriteLine("file length " + f.Length);
		Check(fourcc(0) == "RIFF" && fourcc(8) == "AVI ", "RIFF/AVI signature");
		Check(u32(4) == f.Length - 8, "RIFF size = file length - 8");
		Check(fourcc(12) == "LIST" && u32(16) == 192 && fourcc(20) == "hdrl", "LIST hdrl 192");
		Check(fourcc(24) == "avih" && u32(28) == 56, "avih 56");
		Check(u32(32) == 1000000 / 25, "avih.dwMicroSecPerFrame");
		Check(u32(44) == 0x10, "avih.dwFlags = AVIF_HASINDEX");
		Check(u32(48) == 3, "avih.dwTotalFrames = 3");
		Check(u32(56) == 1, "avih.dwStreams = 1");
		Check(u32(60) == 2000, "avih.dwSuggestedBufferSize = largest frame");
		Check(u32(64) == 320 && u32(68) == 240, "avih width/height");
		Check(fourcc(88) == "LIST" && u32(92) == 116 && fourcc(96) == "strl", "LIST strl 116");
		Check(fourcc(100) == "strh" && u32(104) == 56, "strh 56");
		Check(fourcc(108) == "vids" && fourcc(112) == "MJPG", "strh fccType/fccHandler");
		Check(u32(128) == 1 && u32(132) == 25, "strh dwScale=1 dwRate=25");
		Check(u32(140) == 3, "strh.dwLength = 3");
		Check(u32(144) == 2000, "strh.dwSuggestedBufferSize = largest frame");
		// AVISTREAMHEADER: dwQuality follows dwSuggestedBufferSize, i.e. PosStreamBufferSize (144) + 4
		Check(u32(148) == 0xFFFFFFFFU, "strh.dwQuality = 0xFFFFFFFF (-1, default)");
		Check(u16(156) == 0 && u16(158) == 0 && u16(160) == 320 && u16(162) == 240, "strh.rcFrame");
		Check(fourcc(164) == "strf" && u32(168) == 40, "strf 40");
		Check(u32(172) == 40 && u32(176) == 320 && u32(180) == 240, "bih size/width/height");
		Check(u16(184) == 1 && u16(186) == 24 && fourcc(188) == "MJPG", "bih planes/bitcount/compression");
		Check(fourcc(212) == "LIST" && fourcc(220) == "movi", "LIST movi at 212/220");

		// walk the movi chunks
		int pos = 224;
		int moviEnd = 220 + (int)u32(216);
		int frame = 0;
		var offsets = new int[frameSizes.Length];
		while (pos < moviEnd)
		{
			Check(fourcc(pos) == "00dc", "frame " + frame + " chunk id at " + pos);
			uint size = u32(pos + 4);
			Check(size == frameSizes[frame], "frame " + frame + " size " + size);
			Check(f[pos + 8] == 0 && f[pos + 8 + (int)size - 1] == (byte)(size - 1), "frame " + frame + " payload intact");
			offsets[frame] = pos - 220;
			pos += 8 + (int)size + (int)(size & 1);
			frame++;
		}
		Check(frame == frameSizes.Length, "movi LIST size covers exactly " + frameSizes.Length + " frames");
		Check(pos == moviEnd, "movi LIST size ends on chunk boundary");

		Check(fourcc(pos) == "idx1" && u32(pos + 4) == 16 * frameSizes.Length, "idx1 header");
		for (int i = 0; i < frameSizes.Length; i++)
		{
			int e = pos + 8 + 16 * i;
			Check(fourcc(e) == "00dc" && u32(e + 4) == 0x10, "idx1[" + i + "] id/flags");
			Check(u32(e + 8) == offsets[i], "idx1[" + i + "] offset " + u32(e + 8) + " == " + offsets[i]);
			Check(u32(e + 12) == frameSizes[i], "idx1[" + i + "] size");
		}
		Check(pos + 8 + 16 * frameSizes.Length == f.Length, "idx1 ends at EOF");

		Console.WriteLine(_checks + " checks: " + (_failures == 0 ? "AVI structure OK" : _failures + " failed"));
		return _failures == 0 ? 0 : 1;
	}
}
