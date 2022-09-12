using DmdataSharp;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Exceptions;
using DynamicData;
using KyoshinEewViewer.Core;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataTelegramPublisher : TelegramPublisher
{
	// FIXME: 苦しい 治す
	public static DmdataTelegramPublisher? Instance { get; private set; }

	// 認可を求めるスコープ
	private static readonly string[] RequiredScope = new[]{
		"contract.list",
		"parameter.earthquake",
		"socket.start",
		"socket.close",
		"telegram.list",
		"telegram.data",
		"telegram.get.earthquake",
		"eew.get.forecast",
		"eew.get.warning",
	};

	// スコープからカテゴリへのマップ
	private static readonly Dictionary<string, InformationCategory[]> CategoryMap = new()
	{
		{
			"telegram.earthquake",
			new[]
			{
				InformationCategory.Earthquake,
				InformationCategory.Tsunami,
			}
		},
		{ "eew.forecast", new[] { InformationCategory.EewForecast } },
		{ "eew.warning", new[] { InformationCategory.EewWarning } },
	};

	// カテゴリからカテゴリへのマップ
	private static readonly Dictionary<InformationCategory, TelegramCategoryV1> TelegramCategoryMap = new()
	{
		{ InformationCategory.Earthquake, TelegramCategoryV1.Earthquake },
		{ InformationCategory.Tsunami, TelegramCategoryV1.Earthquake },
		{ InformationCategory.Typhoon, TelegramCategoryV1.Weather },
		{ InformationCategory.EewForecast, TelegramCategoryV1.EewForecast },
		{ InformationCategory.EewWarning, TelegramCategoryV1.EewWarning },
	};

	// カテゴリからタイプ郡へのマップ
	private static readonly Dictionary<InformationCategory, string[]> TypeMap = new()
	{
		{
			InformationCategory.Earthquake,
			new[]
			{
				"VXSE51",
				"VXSE52",
				"VXSE53",
				"VXSE61",
			}
		},
		{
			InformationCategory.EewForecast,
			new[]
			{
				"VXSE42",
				"VXSE44",
				"VXSE45",
			}
		},
		{ InformationCategory.EewWarning, new[] { "VXSE43" } },
		{
			InformationCategory.Tsunami,
			new[]
			{
				"VTSE41",
				"VTSE51",
				"VTSE52",
			}
		},
	};

	private DmdataDistributorApiClientBuilder ClientBuilder { get; } = DmdataDistributorApiClientBuilder.Default
			.Referrer(new Uri("https://www.ingen084.net/"))
			.UserAgent($"KEVi_{Utils.Version};@ingen084");
	private OAuthCredential? Credential { get; set; }
	private DmdataDistributorV2ApiClient? ApiClient { get; set; }
	private DmdataDistributorV2Socket? Socket { get; set; }
	private string? CursorToken { get; set; }

	/// <summary>
	/// 購読中のカテゴリ
	/// </summary>
	public ObservableCollection<InformationCategory> SubscribingCategories { get; } = new();

	private ILogger Logger { get; } = LoggingService.CreateLogger<DmdataTelegramPublisher>();

	private Random Random { get; } = new Random();
	private Timer PullTimer { get; }
	private Timer SettingsApplyTimer { get; }
	private int ReconnectBackoffTime { get; set; } = 10;
	public Timer WebSocketReconnectTimer { get; }

	public DmdataTelegramPublisher()
	{
		PullTimer = new(async s => await PullFeedAsync());
		SettingsApplyTimer = new(async _ =>
		{
			if (ApiClient == null)
				return;
			await StartInternalAsync();
		});
		WebSocketReconnectTimer = new(async s =>
		{
			if (ApiClient != null && SubscribingCategories.Any() && ConfigurationService.Current.Dmdata.UseWebSocket && !(Socket?.IsConnected ?? false))
			{
				Logger.LogInformation("WebSocketへの再接続を試みます");
				await StartInternalAsync();
				ReconnectBackoffTime = Math.Min(600, ReconnectBackoffTime * 2);
			}
			WebSocketReconnectTimer?.Change(TimeSpan.FromSeconds(ReconnectBackoffTime), Timeout.InfiniteTimeSpan);
		}, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
		Instance = this;
	}

	public async Task<EarthquakeStationParameterResponse?> GetEarthquakeStationsAsync()
	{
		if (ApiClient is null)
			return null;
		return await ApiClient.GetEarthquakeStationParameterAsync();
	}

	public override Task InitalizeAsync()
	{
		// 設定ファイルから読み出し
		if (ConfigurationService.Current.Dmdata.RefreshToken != null)
		{
			Credential = new OAuthRefreshTokenCredential(
				ClientBuilder.HttpClient,
				RequiredScope,
				ConfigurationService.Current.Dmdata.OAuthClientId,
				ConfigurationService.Current.Dmdata.RefreshToken);
			ClientBuilder.UseOAuth(Credential);
			ApiClient = ClientBuilder.BuildV2ApiClient();
		}
		else if (!string.IsNullOrWhiteSpace(ConfigurationService.Current.Dmdata.OAuthClientSecret))
		{
			Credential = new OAuthClientCredential(
				ClientBuilder.HttpClient,
				RequiredScope,
				ConfigurationService.Current.Dmdata.OAuthClientId,
				ConfigurationService.Current.Dmdata.OAuthClientSecret);
			ClientBuilder.UseOAuth(Credential);
			ApiClient = ClientBuilder.BuildV2ApiClient();
		}
		else if (ConfigurationService.Current.Dmdata.IsDistributor)
		{
			this.AuthorizeByDistributor();
		}
		else
			return Task.CompletedTask;

		ConfigurationService.Current.Dmdata.WhenAnyValue(x => x.UseWebSocket, x => x.ReceiveTraining)
			.Skip(1) // 起動時に1回イベントが発生してしまうのでスキップする
			.Subscribe(_ => SettingsApplyTimer.Change(1000, Timeout.Infinite));

		return Task.CompletedTask;
	}

	public async Task AuthorizeAsync(CancellationToken cancellationToken)
	{
		var credentials = await SimpleOAuthAuthenticator.AuthorizationAsync(
			ClientBuilder.HttpClient,
			ConfigurationService.Current.Dmdata.OAuthClientId,
			RequiredScope,
			"KyoshinEewViewer for ingen",
			url => UrlOpener.OpenUrl(url),
			token: cancellationToken);
		// 認可でリフレッシュトークンを更新
		Credential = credentials;
		ConfigurationService.Current.Dmdata.RefreshToken = credentials.RefreshToken;
		ClientBuilder.UseOAuth(Credential);
		ApiClient = ClientBuilder.BuildV2ApiClient();
		// 更新通知を流しプロバイダを切り替えてもらう
		OnInformationCategoryUpdated();
	}

	public void AuthorizeByDistributor()
	{
		ConfigurationService.Current.Dmdata.IsDistributor = true;
		var httpClient = new HttpClient(new HttpClientHandler()
		{
#if NET472 || NETSTANDARD2_0
			AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
#else
			AutomaticDecompression = System.Net.DecompressionMethods.All
#endif
		});
		ApiClient = new DmdataDistributorV2ApiClient(httpClient, new DmdataSharp.Authentication.NoneAuthenticator(), ConfigurationService.Current.Dmdata.APIHost);
		// 更新通知を流しプロバイダを切り替えてもらう
		OnInformationCategoryUpdated();
	}

	public Task UnauthorizeAsync()
		=> FailAsync();

	public async override Task<InformationCategory[]> GetSupportedCategoriesAsync()
	{
		if (Credential == null && !ConfigurationService.Current.Dmdata.IsDistributor)
			return Array.Empty<InformationCategory>();
		if (ApiClient == null)
			throw new InvalidOperationException("ApiClientが初期化されていません");

		if (ConfigurationService.Current.Dmdata.IsDistributor)
			return new InformationCategory[] { InformationCategory.Earthquake, InformationCategory.Tsunami, InformationCategory.EewForecast, InformationCategory.EewWarning };

		try
		{
			var contracts = await ApiClient.GetContractListAsync();

			// 必須スコープが存在することを確認する
			if (contracts.Status != "ok")
			{
				Logger.LogError(
					"contract.list に失敗しました。status:{status} code:{code} message:{message}",
					contracts.Status,
					contracts.Error?.Code,
					contracts.Error?.Message
				);
				await FailAsync();
				return Array.Empty<InformationCategory>();
			}

			return contracts.Items.Where(c => c.IsValid && CategoryMap.ContainsKey(c.Classification))
				.Select(s => s.Classification)
				.SelectMany(s => CategoryMap[s]).ToArray();
		}
		catch (DmdataException ex)
		{
			Logger.LogError(ex, "contract.list に失敗しました");
			await FailAsync();
			return Array.Empty<InformationCategory>();
		}
	}

	private bool IsStarting { get; set; }

	private int FailCount { get; set; }
	private int? LastConnectedWebSocketId { get; set; }
	private bool WebSocketDisconnecting { get; set; }
	private async Task StartWebSocketAsync()
	{
		if (WebSocketDisconnecting || IsStarting)
			return;
		if (ApiClient == null)
			throw new InvalidOperationException("ApiClientが初期化されていません");
		if (Socket?.IsConnected ?? false)
			throw new DmdataException("すでにWebSocketに接続しています");

		Logger.LogInformation("WebSocketに接続します");
		IsStarting = true;
		try
		{
			await SwitchInformationAsync(true);

			Socket = new DmdataDistributorV2Socket(ApiClient);
			Socket.Connected += (s, e) =>
			{
				Logger.LogInformation("WebSocket Connected id: {SocketId}", e?.SocketId);
				LastConnectedWebSocketId = e?.SocketId;
				ReconnectBackoffTime = 10;
			};
			Socket.DataReceived += async (s, e) =>
			{
				try
				{
					if (e is null)
					{
						Logger.LogError("WebSocketデータがnullです");
						return;
					}
					if (e.XmlReport is null)
					{
						Logger.LogError("WebSocket電文 {Id} の XMLReport がありません", e.Id);
						return;
					}
					if (e.XmlReport.Head.Title is null)
					{
						Logger.LogError("WebSocket電文 {Id} の Title が取得できません", e.Id);
						return;
					}
					FailCount = 0;

					if (!TypeMap.Any(c => c.Value.Contains(e.Head.Type)))
						return;
					var category = TypeMap.First(c => c.Value.Contains(e.Head.Type)).Key;
					if (!SubscribingCategories.Contains(category))
						return;

					if (category == InformationCategory.EewForecast || category == InformationCategory.EewWarning)
					{
						// EEWはディスクにキャシュしない
						OnTelegramArrived(
							category,
							new Telegram(
								e.Id,
								e.XmlReport.Head.Title,
								e.XmlReport.Control.DateTime,
								() => Task.FromResult(e.GetBodyStream()),
								null
							)
						);
						return;
					}
					await InformationCacheService.CacheTelegramAsync(e.Id, () => e.GetBodyStream());
					OnTelegramArrived(
						category,
						new Telegram(
							e.Id,
							e.XmlReport.Head.Title,
							e.XmlReport.Control.DateTime,
							() => InformationCacheService.TryGetOrFetchTelegramAsync(e.Id, async () => await FetchContentAsync(e.Id)),
							() => InformationCacheService.DeleteTelegramCache(e.Id)
						)
					);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "WebSocketデータ処理中に例外");
				}
			};
			Socket.Error += async (s, e) =>
			{
				if (e is null)
				{
					Logger.LogError("WebSocketエラーがnullです");
					return;
				}
				Logger.LogError("WebSocketエラー受信: {Error}({Code})", e.Error, e.Code);

				// エラーコードの上位2桁で判断する
				switch (e.Code / 100)
				{
					// リクエストに関連するエラー 手動での切断 契約終了の場合はPULL型に変更
					case 44:
					case 48:
						WebSocketDisconnecting = true;
						if (!e.Close)
							await Socket.DisconnectAsync();
						OnFailed(SubscribingCategories.ToArray(), true);
						await StartPullAsync();
						return;
				}
			};
			Socket.Disconnected += async (s, e) =>
			{
				Logger.LogInformation($"WebSocketから切断されました");
				// 4回以上失敗していたらPULLに移行する
				FailCount++;
				if (FailCount >= 4)
				{
					WebSocketDisconnecting = true;
					OnFailed(SubscribingCategories.ToArray(), true);
					await StartPullAsync();
					return;
				}
				OnFailed(SubscribingCategories.ToArray(), true);
				await Task.Delay(1000); // ちょっと間を持たせる
				await StartWebSocketAsync();
			};
			WebSocketDisconnecting = false;
			if (LastConnectedWebSocketId is int lastId)
				try
				{
					await ApiClient.CloseSocketAsync(lastId);
				}
				catch (DmdataApiErrorException) { }

			var classifications = SubscribingCategories.Select(c => TelegramCategoryMap[c]).Distinct().ToArray();
			await Socket.ConnectAsync(new DmdataSharp.ApiParameters.V2.SocketStartRequestParameter(classifications)
			{
				AppName = $"KEVi v{Utils.Version}",
				Types = SubscribingCategories.Where(c => TypeMap.ContainsKey(c)).SelectMany(c => TypeMap[c]).ToArray(),
				Test = ConfigurationService.Current.Dmdata.ReceiveTraining ? "including" : "no",
			});
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "WebSocket接続中に例外が発生したためPULL型に切り替えます");
			OnFailed(SubscribingCategories.ToArray(), true);
			await StartPullAsync();
		}
		finally
		{
			IsStarting = false;
		}
	}
	private async Task StartPullAsync()
	{
		if (IsStarting)
			return;
		IsStarting = true;
		try
		{
			Logger.LogInformation("PULLを開始します");
			if (!SubscribingCategories.Any(c => c != InformationCategory.EewForecast && c != InformationCategory.EewWarning))
			{
				Logger.LogInformation("PULLできるカテゴリが存在しなかったため何もしません");
				return;
			}
			var interval = await SwitchInformationAsync(false);
			PullTimer.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Current.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "PULL開始中にエラーが発生しました");
			await FailAsync();
		}
		finally
		{
			IsStarting = false;
		}
	}

	private async Task<int> SwitchInformationAsync(bool isWebSocket)
	{
		CursorToken = null;
		ReceivedTelegrams.Clear();

		var interval = 1000;

		foreach (var c in SubscribingCategories)
		{
			// EEWは過去の情報を引っ張ってくる意味がないのでスキップ
			if (c == InformationCategory.EewForecast || c == InformationCategory.EewWarning)
			{
				// WSで接続時のみ扱い可能とする、それ以外の場合は開始したことを通知しない
				if (isWebSocket)
					OnHistoryTelegramArrived(
						$"DM-D.S.S(WS)",
						c,
						Array.Empty<Telegram>());
				continue;
			}

			(var infos, interval) = await FetchListAsync(c, false);
			OnHistoryTelegramArrived(
				$"DM-D.S.S({(isWebSocket ? "WS" : "PULL")})",
				c,
				infos.Select(r => new Telegram(
					r.key,
					r.title,
					r.arrivalTime,
					() => InformationCacheService.TryGetOrFetchTelegramAsync(r.key, () => FetchContentAsync(r.key)),
					() => InformationCacheService.DeleteTelegramCache(r.key)
				)).ToArray());
			await Task.Delay(interval);
		}
		return interval;
	}

	private async Task PullFeedAsync()
	{
		try
		{
			if (Socket?.IsConnected ?? false)
			{
				Logger.LogWarning("WebSocket接続中にPullしようとしました");
				return;
			}
			var (infos, interval) = await FetchListAsync(null, true);

			foreach (var (key, title, type, arrivalTime) in infos.Reverse())
			{
				if (!TypeMap.Any(c => c.Value.Contains(type)))
					continue;
				var category = TypeMap.First(c => c.Value.Contains(type)).Key;
				if (!SubscribingCategories.Contains(category))
					continue;

				OnTelegramArrived(
					category,
					new Telegram(
						key,
						title,
						arrivalTime,
						() => InformationCacheService.TryGetOrFetchTelegramAsync(key, () => FetchContentAsync(key)),
						() => InformationCacheService.DeleteTelegramCache(key)
					)
				);
			}

			// レスポンスの時間*設定での倍率のランダム間隔でリクエストを行う
			PullTimer?.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Current.Dmdata.PullMultiply, 1)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "PULL受信中にエラーが発生しました");
			await FailAsync();
		}
	}

	private List<string> ReceivedTelegrams { get; } = new();
	private async Task<((string key, string title, string type, DateTime arrivalTime)[], int nextPoolingInterval)> FetchListAsync(InformationCategory? filterCategory, bool useCursorToken)
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClientが初期化されていません");
		System.Diagnostics.Debug.WriteLine($"FetchListAsync {filterCategory} {useCursorToken}");

		var result = new List<(string key, string title, string type, DateTime arrivalTime)>();

		Logger.LogDebug("get telegram list CursorToken: {CursorToken}", CursorToken);
		var resp = await ApiClient.GetTelegramListAsync(
			type: filterCategory is InformationCategory ca ? string.Join(",", TypeMap[ca]) : null,
			xmlReport: true,
			test: ConfigurationService.Current.Dmdata.ReceiveTraining ? "including" : "no",
			cursorToken: useCursorToken ? CursorToken : null,
			limit: 50
		);

		// TODO: リトライ処理の実装
		if (resp.Status != "ok")
			throw new DmdataException($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

		Logger.LogDebug("dmdata items count: {count}", resp.Items.Length);
		foreach (var item in resp.Items)
		{
			// 解析すべき情報だけ取ってくる
			if (item.Format != "xml" || ReceivedTelegrams.Contains(item.Id))
				continue;

			result.Add((
				item.Id,
				item.XmlReport!.Head.Title!,
				item.Head.Type,
				item.XmlReport!.Head.ReportDateTime));

			if (!useCursorToken)
				ReceivedTelegrams.Add(item.Id);
		}
		if (useCursorToken)
		{
			CursorToken = resp.NextPooling;
			ReceivedTelegrams.Clear();
		}

		Logger.LogDebug("get telegram list nextpooling: {interval}", resp.NextPoolingInterval);
		if (result.Any())
			result.Reverse();
		return (result.ToArray(), resp.NextPoolingInterval);
	}

	private async Task<Stream> FetchContentAsync(string key)
	{
		var count = 0;
		while (true)
		{
			count++;
			try
			{
				Logger.LogInformation("dmdataから取得しています: {key}", key);
				return await (ApiClient?.GetTelegramStreamAsync(key) ?? throw new Exception("ApiClientが初期化されていません"));
			}
			catch (DmdataRateLimitExceededException ex)
			{
				Logger.LogWarning("レートリミットに引っかかっています try{count} ({RetryAfter})", count, ex.RetryAfter);
				if (count > 10)
					throw;
				await Task.Delay(200);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "電文取得中にエラーが発生しました");
				await FailAsync();
				throw;
			}
		}
	}

	public async override void Start(InformationCategory[] categories)
	{
		// 新規追加するもののみ抽出
		var added = categories.Where(c => !SubscribingCategories.Contains(c));
		if (!added.Any())
			return;
		/// 追加があった場合、接続し直す
		SubscribingCategories.AddRange(added);
		await StartInternalAsync();
	}

	/// <summary>
	/// WebSocketの接続状況を設定に同期する
	/// </summary>
	/// <returns></returns>
	public async Task StartInternalAsync()
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClient が初期化されていません");

		// 停止
		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		if (Socket?.IsConnected ?? false)
		{
			WebSocketDisconnecting = true;
			await Socket.DisconnectAsync();
		}
		Socket = null;

		// 開始
		if (ConfigurationService.Current.Dmdata.UseWebSocket)
		{
			WebSocketDisconnecting = false;
			await StartWebSocketAsync();
		}
		else
			await StartPullAsync();
	}

	public async override void Stop(InformationCategory[] categories)
	{
		SubscribingCategories.RemoveMany(SubscribingCategories.Where(c => categories.Contains(c)).ToArray());
		if (!SubscribingCategories.Any())
			await StopInternalAsync();
	}

	private async Task StopInternalAsync()
	{
		WebSocketDisconnecting = true;
		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		if (Socket?.IsConnected ?? false)
			await Socket.DisconnectAsync();
		Socket = null;
		ApiClient = null;
	}

	/// <summary>
	/// 速やかに認可情報を失効させ、処理を終了する
	/// </summary>
	private async Task FailAsync()
	{
		await StopInternalAsync();
		try
		{
			Credential?.RevokeRefreshTokenAsync();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "失効時のリフレッシュトークンの無効化に失敗しました");
		}
		Credential = null;
		ConfigurationService.Current.Dmdata.RefreshToken = null;

		OnFailed(SubscribingCategories.ToArray(), false);
		SubscribingCategories.Clear();
	}
}
