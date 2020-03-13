﻿using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using KyoshinMonitorLib.UrlGenerator;
using MessagePack;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class KyoshinMonitorWatchService
	{
		private AppApi AppApi { get; set; }
		private WebApi WebApi { get; set; }
		private ObservationPoint[] Points { get; set; }
		private List<Models.Eew> EewCache { get; } = new List<Models.Eew>();

		private LoggerService Logger { get; }
		private ConfigurationService ConfigService { get; }
		private TrTimeTableService TrTimeTableService { get; }
		private TimerService TimerService { get; }

		private Events.RealTimeDataUpdated RealTimeDataUpdatedEvent { get; }
		private Events.EewUpdated EewUpdatedEvent { get; }

		public KyoshinMonitorWatchService(
			LoggerService logger,
			TrTimeTableService trTimeTableService,
			ConfigurationService configService,
			TimerService timeService,
			IEventAggregator aggregator)
		{
			Logger = logger;
			ConfigService = configService;
			TrTimeTableService = trTimeTableService;
			TimerService = timeService;

			RealTimeDataUpdatedEvent = aggregator.GetEvent<Events.RealTimeDataUpdated>();
			EewUpdatedEvent = aggregator.GetEvent<Events.EewUpdated>();
			TimerService.MainTimerElapsed += TimerElapsed;
		}

		public async void Start()
		{
			Logger.OnWarningMessageUpdated("初期化中...");
			Logger.Info("観測点情報を読み込んでいます。");
			var points = MessagePackSerializer.Deserialize<ObservationPoint[]>(Properties.Resources.ShindoObsPoints, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
			AppApi = new AppApi(points) { Timeout = TimeSpan.FromSeconds(2) };
			Points = points;

			Logger.Info("走時表を準備しています。");
			TrTimeTableService.Initalize();

			await TimerService.StartMainTimerAsync();
			Logger.OnWarningMessageUpdated($"初回のデータ取得中です。しばらくお待ち下さい。");
		}

		private async Task TimerElapsed(DateTime realTime)
		{
			var time = realTime;
			// タイムシフト中なら加算します(やっつけ)
			if (ConfigService.Configuration.Timer.TimeshiftSeconds > 0)
				time = time.AddSeconds(-ConfigService.Configuration.Timer.TimeshiftSeconds);

			try
			{
				var eventData = new Events.RealTimeDataUpdated { Time = time };

				async Task<bool> ParseUseImage()
				{
					try
					{
						//失敗したら画像から取得
						var result = await WebApi.ParseIntensityFromParameterAsync(Points, time);
						if (result?.StatusCode != System.Net.HttpStatusCode.OK)
						{
							if (ConfigService.Configuration.Timer.TimeshiftSeconds > 0)
							{
								Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} 利用できませんでした。({result?.StatusCode})");
								return false;
							}
							if (ConfigService.Configuration.Timer.AutoOffsetIncrement)
							{
								Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} オフセットを調整しました。");
								ConfigService.Configuration.Timer.Offset = Math.Min(5000, ConfigService.Configuration.Timer.Offset + 100);
								return false;
							}

							Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} オフセットを調整してください。");
							return false;
						}
						eventData.Data = result.Data.Where(r => r.AnalysisResult != null).Select(r => new LinkedRealTimeData(new LinkedObservationPoint(null, r.ObservationPoint), r.AnalysisResult)).ToArray();
						eventData.IsUseAlternativeSource = true;
						//Debug.WriteLine("Image Count: " + result.Data.Count(d => d.AnalysisResult != null));
					}
					catch (KyoshinMonitorException ex)
					{
						Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} 画像ソース利用不可({ex.Message})");
						return false;
					}
					return true;
				}

				if (ConfigService.Configuration.KyoshinMonitor.UseImageParse && ConfigService.Configuration.KyoshinMonitor.AlwaysImageParse)
					await ParseUseImage();
				else
				{
					//APIで取得
					var shindoResult = await AppApi.GetLinkedRealTimeData(time, RealTimeDataType.Shindo);
					if (shindoResult?.Data != null)
					{
						eventData.Data = shindoResult.Data;
						eventData.IsUseAlternativeSource = false;
						System.Diagnostics.Debug.WriteLine("Missed Count: " + shindoResult.Data.Count(d => d.ObservationPoint.Point == null));
					}
					else if (ConfigService.Configuration.KyoshinMonitor.UseImageParse)
						await ParseUseImage();
					else
						Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} オフセット調整または画像を利用してください。");
				}

				try
				{
					var eewResult = await WebApi.GetEewInfo(time);
					var isEewUpdated = false;

					if (ConfigService.Configuration.Timer.TimeshiftSeconds > 0 && EewCache.Count > 0) {
						var removes = new List<Models.Eew>();
						foreach (var e in EewCache)
							if (e.UpdatedTime > time)
								removes.Add(e);
						foreach (var e in removes)
							EewCache.Remove(e);
					}


					if (!string.IsNullOrEmpty(eewResult.Data?.CalcintensityString))
					{
						var eew = EewCache.FirstOrDefault(e => e.Id == eewResult.Data.ReportId);
						if (eew != null)
						{
							eew.UpdatedTime = time;
							if (eew.Count != (eewResult.Data.ReportNum ?? 0))
							{
								eew.Place = eewResult.Data.RegionName;
								eew.IsCancelled = eewResult.Data.IsCancel ?? false;
								eew.IsFinal = eewResult.Data.IsFinal ?? false;
								eew.Count = eewResult.Data.ReportNum ?? 0;
								eew.Depth = eewResult.Data.Depth ?? 0;
								eew.Intensity = eewResult.Data.Calcintensity;
								eew.IsWarning = eewResult.Data.IsAlert;
								eew.Magnitude = eewResult.Data.Magunitude ?? 0;
								eew.OccurrenceTime = eewResult.Data.OriginTime ?? DateTime.Now;
								eew.ReceiveTime = eewResult.Data.ReportTime ?? DateTime.Now;
								eew.Location = eewResult.Data.Location;
								isEewUpdated = true;
							}
						}
						else
						{
							eew = new Models.Eew
							{
								Id = eewResult.Data.ReportId,
								Place = eewResult.Data.RegionName,
								IsCancelled = eewResult.Data.IsCancel ?? false,
								IsFinal = eewResult.Data.IsFinal ?? false,
								Count = eewResult.Data.ReportNum ?? 0,
								Depth = eewResult.Data.Depth ?? 0,
								Intensity = eewResult.Data.Calcintensity,
								IsWarning = eewResult.Data.IsAlert,
								Magnitude = eewResult.Data.Magunitude ?? 0,
								OccurrenceTime = eewResult.Data.OriginTime ?? DateTime.Now,
								ReceiveTime = eewResult.Data.ReportTime ?? DateTime.Now,
								Location = eewResult.Data.Location,
								UpdatedTime = time,
							};
							EewCache.Add(eew);
							isEewUpdated = true;
						}
					}
					else if (EewCache.Count == 1 && !EewCache[0].IsFinal && !EewCache[0].IsCancelled) // EEWキャッシュが1件のときのみキャンセルを処理
					{
						EewCache[0].IsCancelled = true;
						isEewUpdated = true;
					}

					if (EewCache.Count > 0)
					{
						var removes = new List<Models.Eew>();
						// 最終アップデートから1分経過したら削除
						foreach (var e in EewCache)
						{
							if ((time - e.UpdatedTime) >= TimeSpan.FromMinutes(1))
								removes.Add(e);
						}
						foreach (var r in removes)
							EewCache.Remove(r);
						isEewUpdated = true;
					}

					if (isEewUpdated)
						EewUpdatedEvent.Publish(new Events.EewUpdated
						{
							Eews = EewCache.ToArray(),
							Time = time
						});
				}
				catch (KyoshinMonitorException)
				{
					Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} EEWの情報が取得できませんでした。");
					Logger.Warning("EEWの情報が取得できませんでした。");
				}
				RealTimeDataUpdatedEvent.Publish(eventData);
			}
			catch (KyoshinMonitorException ex) when (ex.Message.Contains("Request Timeout"))
			{
				Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} タイムアウトしました。");
				Logger.Warning("取得にタイムアウトしました。");
			}
			catch (KyoshinMonitorException ex)
			{
				Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} {ex.Message}");
				Logger.Warning("取得にタイムアウトしました。");
			}
			catch (Exception ex)
			{
				Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} 汎用エラー({ex.Message})");
				Logger.Warning("汎用エラー\n" + ex);
			}
		}
	}
}