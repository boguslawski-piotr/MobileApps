﻿#if !NETSTANDARD1_6 && !WINDOWS_UWP

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace pbXNet
{
	public class BinarySerializer : ISerializer
	{
		static readonly Lazy<IFormatter> _formatter = new Lazy<IFormatter>(() => new BinaryFormatter());

		public string Serialize<T>(T o, string id = null)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				_formatter.Value.Serialize(stream, o);
				string d = ConvertEx.ToHexString(stream);

				if (id != null)
				{
					d = id + "_" + d + "_";
				}

				return d;
			}
		}

		public T Deserialize<T>(string d, string id = null)
		{
			if (id != null)
			{
				Match m = Regex.Match(d, id + "_[0-9A-F]*_");
				if(m.Success)
					d = d.Substring(m.Index + id.Length + 1, m.Length - (id.Length + 2));
			}

			using (MemoryStream stream = new MemoryStream(ConvertEx.FromHexString(d)))
			{
				object o = _formatter.Value.Deserialize(stream);
				return (T)o;
			}
		}
	}
}

#endif