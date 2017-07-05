﻿using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace pbXNet
{
	public class DeflateCompressor : ICompressor
	{
		public T Compress<T>(Stream from) where T : Stream, new()
		{
			if (from == null)
				throw new ArgumentNullException(nameof(from));

			T to = new T();
			using (DeflateStream via = new DeflateStream(to, CompressionMode.Compress, true))
			{
				from.Position = 0;
				from.CopyTo(via);
			}
			return to;
		}

		public T Decompress<T>(Stream from) where T : Stream, new()
		{
			if (from == null)
				throw new ArgumentNullException(nameof(from));

			T to = new T();
			using (DeflateStream via = new DeflateStream(from, CompressionMode.Decompress, true))
			{
				from.Position = 0;
				via.CopyTo(to);
			}
			return to;
		}

		public string Compress(string d, bool returnAsBase64 = false)
		{
			if (d == null)
				throw new ArgumentNullException(nameof(d));

			MemoryStream dcs = Compress<MemoryStream>(ConvertEx.ToMemoryStream(d));
			dcs.Position = 0;
			byte[] dca = dcs.ToArray();
			dcs.Dispose();

			if (!returnAsBase64)
				d = ConvertEx.ToHexString(dca);
			else
				d = Convert.ToBase64String(dca);
			return d;
		}

		public string Decompress(string d, bool fromBase64 = false)
		{
			if (d == null)
				throw new ArgumentNullException(nameof(d));

			MemoryStream dms = new MemoryStream(!fromBase64 ? ConvertEx.FromHexString(d) : Convert.FromBase64String(d));
			MemoryStream dcs = Decompress<MemoryStream>(dms);

			dcs.Position = 0;
			d = ConvertEx.ToString(dcs);

			dcs.Dispose();
			dms.Dispose();
			return d;
		}
	}
}

