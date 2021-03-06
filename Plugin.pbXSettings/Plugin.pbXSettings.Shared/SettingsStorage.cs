﻿using System;
using Plugin.pbXSettings.Abstractions;

namespace Plugin.pbXSettings
{
	/// <summary>
	/// Class giving access to native settings storage.
	/// </summary>
	public static class SettingsStorage
	{
		/// <summary>
		/// Indicates whether the current platform supports settings storage.
		/// </summary>
		public static bool IsSupported => _impl.Value == null ? false : true;

		/// <summary>
		/// Current native settings storage.
		/// </summary>
		/// <seealso cref="Abstractions.ISettingsStorage"/>
		public static ISettingsStorage Current
		{
			get {
				ISettingsStorage ss = _impl.Value;
				if (ss == null)
					throw NotImplementedInReferenceAssembly();
				return ss;
			}
		}

		static Lazy<ISettingsStorage> _impl = new Lazy<ISettingsStorage>(() => CreateSettingsStorage(), System.Threading.LazyThreadSafetyMode.PublicationOnly);

		static ISettingsStorage CreateSettingsStorage()
		{
#if __PCL__
            return null;
#else
			return (ISettingsStorage)new SettingsStorageImplementation();
#endif
		}

		internal static Exception NotImplementedInReferenceAssembly() =>
			new NotImplementedException("This functionality is not implemented in the portable version of this assembly. You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
	}
}
