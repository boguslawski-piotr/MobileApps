﻿using System;
namespace pbXForms
{
	public class Metrics
	{
#if __IOS__
		public static double StatusBarHeight = 20;
#endif
#if __ANDROID__
		public static double StatusBarHeight = 24;
#endif
		public static double ScreenEdgeMargin = 16;

		public static double AppBarHeightPortrait = 56;
		public static double AppBarHeightLandscape = 48;

		public static double ToolBarHeightPortrait = 48;
		public static double ToolBarHeightLandscape = 48;

		public static double ToolBarItemsSpacing = 2;
		public static double ToolBarItemsWideSpacing = 16;

		public static double ButtonItemsSpacing = 8;

		public static double TouchTargetHeight = 48;
		public static double IconHeight = 24;
	}
}
