﻿using KyoshinEewViewer.Map.Layers.ImageTile;
using KyoshinEewViewer.Services;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace KyoshinEewViewer.Series.Radar;

public class RadarImageTileProvider : ImageTileProvider
{
	private RadarSeries Series { get; }
	private DateTime BaseTime { get; }
	private DateTime ValidTime { get; }
	public RadarImageTileProvider(RadarSeries series, DateTime baseTime, DateTime validTime)
	{
		Series = series;
		BaseTime = baseTime;
		ValidTime = validTime;
	}

	private ConcurrentDictionary<(int z, int x, int y), SKBitmap?> Cache { get; } = new();

	public int MinZoomLevel { get; } = 4;
	public int MaxZoomLevel { get; } = 10;

	public override int GetTileZoomLevel(double zoom)
		=> Math.Clamp((int)zoom, MinZoomLevel, MaxZoomLevel) / 2 * 2;

	public void OnImageUpdated((int z, int x, int y) loc, SKBitmap bitmap)
	{
		if (IsDisposed)
		{
			bitmap.Dispose();
			return;
		}
		Cache[loc] = bitmap;
		if (bitmap != null)
			OnImageFetched();
	}

	public override bool TryGetTileBitmap(int z, int x, int y, bool doNotFetch, out SKBitmap? bitmap)
	{
		var sw = Stopwatch.StartNew();
		void DW(string message)
		{
			if (sw.ElapsedMilliseconds != 0)
				Debug.WriteLine($"TryGetTileBitmap {message} {sw.Elapsed.TotalMilliseconds:0.00}ms");
		}
		var loc = (z, x, y);
		if (Cache.TryGetValue(loc, out bitmap))
		{
			DW("in-memory cache");
			return true;
		}
		var url = $"https://www.jma.go.jp/bosai/jmatile/data/nowc/{BaseTime:yyyyMMddHHmm00}/none/{ValidTime:yyyyMMddHHmm00}/surf/hrpns/{z}/{x}/{y}.png";
		if (InformationCacheService.GetImage(url) is SKBitmap bitmap2)
		{
			DW("disk cache");
			Cache[loc] = bitmap = bitmap2;
			return true;
		}
		if (doNotFetch)
			return false;

		// 重複リクエスト防止はFetchImage側でやるので気軽に投げる
		Series.FetchImage(this, loc, url);
		DW("fetch");
		return false;
	}

	public override void Dispose()
	{
		IsDisposed = true;
		lock (this)
		{
			foreach (var b in Cache.Values)
				b?.Dispose();
			Cache.Clear();
		}
		GC.SuppressFinalize(this);
	}
}
