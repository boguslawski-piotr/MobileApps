﻿#if __IOS__

using UIKit;
using Xamarin.Forms;

namespace pbXForms
{
	public static partial class DeviceEx
	{
		static DeviceOrientation _Orientation
		{
			get {
				var currentDeviceOrientation = UIDevice.CurrentDevice.Orientation;
				if (currentDeviceOrientation != UIDeviceOrientation.Unknown)
				{
					// TODO: czemu UIDevice.CurrentDevice.Orientation nie dziala?
				}

				var currentOrientation = UIApplication.SharedApplication.StatusBarOrientation;

				bool isPortrait =
					currentOrientation == UIInterfaceOrientation.Portrait
					|| currentOrientation == UIInterfaceOrientation.PortraitUpsideDown;

				return isPortrait ? DeviceOrientation.Portrait : DeviceOrientation.Landscape;
			}
		}

		static bool _StatusBarVisible
		{
			get {
				return
					Device.Idiom != TargetIdiom.Phone
					|| DeviceEx.Orientation != DeviceOrientation.Landscape;
			}
		}
	}
}

#endif
