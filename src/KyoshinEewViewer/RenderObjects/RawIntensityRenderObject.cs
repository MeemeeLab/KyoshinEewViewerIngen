﻿using KyoshinEewViewer.CustomControls;
using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.RenderObjects
{
	public class RawIntensityRenderObject : IRenderObject
	{
		private static Typeface TypeFace { get; } = new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Gen Shin Gothic P Medium"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

		public string Name { get; }
		public FormattedText NameFormattedText { get; }
		public RawIntensityRenderObject(Location location, string name, float rawIntensity = float.NaN)
		{
			Location = location ?? throw new ArgumentNullException(nameof(location));
			RawIntensity = rawIntensity;
			Name = name;
		}

		/// <summary>
		/// 地理座標
		/// </summary>
		public Location Location { get; set; }

		/// <summary>
		/// 生の震度の値
		/// </summary>
		public double RawIntensity { get; set; }

		private Color intensityColor;
		/// <summary>
		/// その地点の色
		/// </summary>
		public Color IntensityColor
		{
			get => intensityColor;
			set
			{
				if (intensityColor == value)
					return;
				intensityColor = value;
				IntensityBrush = null;
			}
		}
		private SolidColorBrush IntensityBrush { get; set; }
		static Pen InvalidatePen { get; set; }

		public void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme)
		{
			var intensity = (float)Math.Min(Math.Max(RawIntensity, -3), 7.0);
			var circleSize = (zoom - 4) * 1.75;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = Location.ToPixel(zoom);
			if (!bound.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			// 震度色のブラシ
			if (IntensityBrush == null && !float.IsNaN(intensity))
			{
				IntensityBrush = new SolidColorBrush(IntensityColor);
				IntensityBrush.Freeze();
			}
			// 無効な観測点のブラシ
			if (InvalidatePen == null)
			{
				InvalidatePen = new Pen(new SolidColorBrush(Colors.Gray), 1);
				InvalidatePen.Freeze();
			}

			context.DrawEllipse(float.IsNaN(intensity) ? null : IntensityBrush, float.IsNaN(intensity) ? InvalidatePen : null, pointCenter - (Vector)leftTopPixel, circleSize, circleSize);
			if (zoom >= 9)
			{
				var text = new FormattedText(zoom >= 9.5 ? (Name + "\n" + (float.IsNaN(intensity) ? "-" : intensity.ToString("0.0"))) : Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TypeFace, 14, isDarkTheme ? Brushes.White : Brushes.Black, 94)
				{
					LineHeight = circleSize * 1.2
				};
				context.DrawText(text, pointCenter - (Vector)leftTopPixel + new Vector(circleSize * 1.5, -circleSize));
			}
		}
	}
}