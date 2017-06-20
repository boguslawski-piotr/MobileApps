using System;

namespace pbXNet
{
	public class OptimizedForStringSerializer : ISerializer
	{
		ISerializer _serializer;

		public OptimizedForStringSerializer(ISerializer serializer)
		{
			_serializer = serializer;
		}

		public virtual string Serialize<T>(T o, string id = null)
		{
			string d;
			if (id == null && typeof(T).Equals(typeof(string)))
				d = o.ToString();
			else
				d = _serializer.Serialize(o, id);

			return d;
		}

		public virtual T Deserialize<T>(string d, string id = null)
		{
			if (id == null && typeof(T).Equals(typeof(string)))
				return (T)(object)d;
			else
				return _serializer.Deserialize<T>(d, id);
		}
	}
}
