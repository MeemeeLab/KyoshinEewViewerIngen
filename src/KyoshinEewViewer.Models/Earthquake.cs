﻿using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Models
{
	/// <summary>
	/// 地震情報
	/// </summary>
	public class Earthquake
	{
		public string Id { get; set; }
		public bool IsSokuhou { get; set; }
		public DateTime OccurrenceTime { get; set; }
		public bool IsReportTime { get; set; }
		public string Place { get; set; }
		public JmaIntensity Intensity { get; set; }
		public float Magnitude { get; set; }
		public int Depth { get; set; }
		public bool IsVeryShallow => Depth <= 0;
		public bool IsNoDepthData => Depth <= -1;
	}
}