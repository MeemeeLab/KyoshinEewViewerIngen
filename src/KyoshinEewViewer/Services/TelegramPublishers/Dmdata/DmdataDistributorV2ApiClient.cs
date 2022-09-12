using DmdataSharp.ApiParameters.V2;
using DmdataSharp.ApiResponses.V2;
using DmdataSharp.ApiResponses.V2.GroupedData;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication;
using DmdataSharp.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


[JsonSerializable(typeof(ContractListResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class ContractListResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(SocketListResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class SocketListResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(SocketStartRequestParameter), GenerationMode = JsonSourceGenerationMode.Serialization)]
internal partial class SocketStartRequestParameterSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(SocketCloseResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class SocketCloseResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(EewEventResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class EewEventResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(SocketStartResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class SocketStartResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(TelegramListResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class TelegramListResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(EarthquakeListResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class EarthquakeListResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(EarthquakeEventResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class EarthquakeEventResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(EewListResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class EewListResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(EarthquakeStationParameterResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class EarthquakeStationParameterResponseSerializerContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(TsunamiStationParameterResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class TsunamiStationParameterResponseSerializerContext : JsonSerializerContext
{
}

namespace DmdataSharp
{
	public class DmdataDistributorV2ApiClient : DmdataV2ApiClient
	{
		private string baseUrl;

		/// <summary>
		/// dmdataのAPI V2クライアントを初期化します
		/// </summary>
		/// <param name="client">内部で使用するHttpClient</param>
		/// <param name="authenticator">使用する認証</param>
		public DmdataDistributorV2ApiClient(HttpClient client, Authenticator authenticator, string baseUrl) : base(client, authenticator) {
			this.baseUrl = baseUrl;
		}

		/// <summary>
		/// 契約中、未契約の情報リストを取得する
		/// <para>contract.list が必要です</para>
		/// </summary>
		/// <returns>契約中、未契約の情報リスト</returns>
		public new Task<ContractListResponse> GetContractListAsync()
			=> GetJsonObject("http://" + this.baseUrl + "/v2/contract", ContractListResponseSerializerContext.Default.ContractListResponse);

		/// <summary>
		/// WebSocketに関するリストを取得する
		/// <para>socket.list が必要です</para>
		/// </summary>
		/// <returns>WebSocketに関するリスト</returns>
		public new Task<SocketListResponse> GetSocketListAsync()
			=> GetJsonObject("http://" + this.baseUrl + "/v2/socket", SocketListResponseSerializerContext.Default.SocketListResponse);
		/// <summary>
		/// WebSocket接続を開始するためのURLを取得する
		/// <para>socket.start/取得する情報に合わせた各権限が必要です</para>
		/// </summary>
		/// <param name="param">接続開始のためのパラメータ</param>
		/// <returns>リクエスト結果</returns>
		public new Task<SocketStartResponse> GetSocketStartAsync(SocketStartRequestParameter param)
			=> PostJsonObject("http://" + this.baseUrl + "/v2/socket", param, SocketStartRequestParameterSerializerContext.Default.SocketStartRequestParameter, SocketStartResponseSerializerContext.Default.SocketStartResponse);
		/// <summary>
		/// WebSocketに関するリストを取得する
		/// <para>socket.close が必要です</para>
		/// </summary>
		/// <returns>成功した場合はnull 失敗した場合はレスポンス</returns>
		public new Task<SocketCloseResponse?> CloseSocketAsync(int id)
			=> DeleteJsonObject("http://" + this.baseUrl + "/v2/socket/" + id, SocketCloseResponseSerializerContext.Default.SocketCloseResponse);

		/// <summary>
		/// 電文リストを取得する
		/// <para>telegram.list が必要です</para>
		/// <para>ポーリングする場合は必ずcursorTokenを使用しnextPoolingInterval以上の間隔でリクエストしてください！</para>
		/// </summary>
		/// <param name="type">検索する電文ヘッダ 前方一致</param>
		/// <param name="xmlReport">XML電文のControl/Headを表示するか</param>
		/// <param name="test">訓練･試験等のテスト等電文を取得するか including: テスト等電文を含む、only: テスト等電文のみ</param>
		/// <param name="cursorToken">前回のレスポンスの値を入れると前回以降の新しい情報のみを取得できる</param>
		/// <param name="formatMode">データフォーマットの指定 生電文: raw、JSON化データ: json</param>
		/// <param name="limit">取得する電文数 最大は100</param>
		/// <returns>電文リスト情報</returns>
		public async new Task<TelegramListResponse> GetTelegramListAsync(
			string? type = null,
			bool xmlReport = false,
			string test = "no",
			string? cursorToken = null,
			string? formatMode = "raw",
			int limit = 20
			)
		{
			var parameterMap = new Dictionary<string, string?>();
			if (!string.IsNullOrWhiteSpace(type))
				parameterMap["type"] = type;
			if (xmlReport)
				parameterMap["xmlReport"] = "true";
			if (test != "no")
				parameterMap["test"] = test;
			if (!string.IsNullOrWhiteSpace(cursorToken))
				parameterMap["cursorToken"] = cursorToken;
			if (formatMode != "raw")
				parameterMap["formatMode"] = formatMode;
			if (limit != 20)
				parameterMap["limit"] = limit.ToString();
			return await GetJsonObject($"http://" + this.baseUrl + $"/v2/telegram?" + await new FormUrlEncodedContent(parameterMap!).ReadAsStringAsync(), TelegramListResponseSerializerContext.Default.TelegramListResponse);
		}

		/// <summary>
		/// 地震イベント一覧を取得します
		/// <para>gd.earthquake が必要です</para>
		/// </summary>
		/// <param name="hypocenter">検索する震央地名コードの3桁の数字</param>
		/// <param name="maxInt">検索する最大震度の下限</param>
		/// <param name="date">検索する地震波検知時刻の日付 時刻部分は使用されません</param>
		/// <param name="cursorToken">前回のレスポンスの値を入れると前回以降の新しい情報のみを取得できる</param>
		/// <param name="limit">返す情報数を指定する 最大は100</param>
		/// <returns>地震イベント一覧</returns>
		public async new Task<EarthquakeListResponse> GetEarthquakeEventsAsync(
			string? hypocenter = null,
			string? maxInt = null,
			DateTime? date = null,
			string? cursorToken = null,
			int? limit = 20
			)
		{
			var parameterMap = new Dictionary<string, string?>();
			if (!string.IsNullOrWhiteSpace(hypocenter))
				parameterMap["hypocenter"] = hypocenter;
			if (!string.IsNullOrWhiteSpace(maxInt))
				parameterMap["maxInt"] = maxInt;
			if (date is DateTime dateTime)
				parameterMap["date"] = dateTime.Date.ToString("yyyy-MM-dd");
			if (!string.IsNullOrWhiteSpace(cursorToken))
				parameterMap["cursorToken"] = cursorToken;
			if (limit != 20)
				parameterMap["limit"] = limit.ToString();
			return await GetJsonObject($"http://" + this.baseUrl + $"/v2/gd/earthquake?" + await new FormUrlEncodedContent(parameterMap!).ReadAsStringAsync(), EarthquakeListResponseSerializerContext.Default.EarthquakeListResponse);
		}

		/// <summary>
		/// 地震イベントの詳細を取得します
		/// <para>gd.earthquake が必要です</para>
		/// </summary>
		/// <param name="eventId">地震情報のEventID</param>
		/// <returns>地震イベントの詳細</returns>
		public new Task<EarthquakeEventResponse> GetEarthquakeEventAsync(string eventId)
			=> GetJsonObject($"http://" + this.baseUrl + $"/v2/gd/earthquake/" + eventId, EarthquakeEventResponseSerializerContext.Default.EarthquakeEventResponse);

		/// <summary>
		/// 緊急地震速報イベント一覧を取得します
		/// <para>gd.eew が必要です</para>
		/// </summary>
		/// <param name="datetimeFrom">検索する最終報発表日時の絞り込みに使う開始日時</param>
		/// <param name="datetimeTo">検索する最終報発表日時の絞り込みに使う終了日時（この時刻は含まない）</param>
		/// <param name="cursorToken">前回のレスポンスの値を入れると前回以降の新しい情報のみを取得できる</param>
		/// <param name="limit">返す情報数を指定する 最大は100</param>
		/// <returns>緊急地震速報イベント一覧</returns>
		public async new Task<EewListResponse> GetEewEventsAsync(
			DateTime? datetimeFrom = null,
			DateTime? datetimeTo = null,
			string? cursorToken = null,
			int? limit = 20
			)
		{
			var parameterMap = new Dictionary<string, string?>();
			if (datetimeFrom is DateTime dateTime)
				parameterMap["date"] = dateTime.Date.ToString("yyyy-MM-dd");
			if (datetimeFrom is DateTime || datetimeTo is DateTime)
			{
				var datetime = string.Empty;
				if (datetimeFrom is DateTime dtFrom) datetime += $"{dtFrom:yyyy-MM-ddTHH:mm:ss}";
				datetime += "~";
				if (datetimeTo is DateTime dtTo) datetime += $"{dtTo:yyyy-MM-ddTHH:mm:ss}";
				parameterMap["datetime"] = datetime;
			}
			if (!string.IsNullOrWhiteSpace(cursorToken))
				parameterMap["cursorToken"] = cursorToken;
			if (limit != 20)
				parameterMap["limit"] = limit.ToString();

			return await GetJsonObject("http://" + this.baseUrl + "/v2/gd/eew?" + await new FormUrlEncodedContent(parameterMap!).ReadAsStringAsync(), EewListResponseSerializerContext.Default.EewListResponse);
		}

		/// <summary>
		/// 緊急地震速報イベントの詳細を取得します
		/// <para>gd.eew が必要です</para>
		/// </summary>
		/// <param name="eventId">緊急地震速報のEventID</param>
		/// <returns>緊急地震速報イベントの詳細</returns>
		public new Task<EewEventResponse> GetEewEventAsync(string eventId)
			=> GetJsonObject($"http://" + this.baseUrl + $"/v2/gd/eew/" + eventId, EewEventResponseSerializerContext.Default.EewEventResponse);

		/// <summary>
		/// 地震観測地点の情報を取得します
		/// </summary>
		/// <returns>地震観測地点の情報</returns>
		public new Task<EarthquakeStationParameterResponse> GetEarthquakeStationParameterAsync()
			=> GetJsonObject("http://" + this.baseUrl + "/v2/parameter/earthquake/station", EarthquakeStationParameterResponseSerializerContext.Default.EarthquakeStationParameterResponse);
		/// <summary>
		/// 津波観測地点の情報を取得します
		/// </summary>
		/// <returns>津波観測地点の情報</returns>
		public new Task<TsunamiStationParameterResponse> GetTsunamiStationParameterAsync()
			=> GetJsonObject("http://" + this.baseUrl + "/v2/parameter/tsunami/station", TsunamiStationParameterResponseSerializerContext.Default.TsunamiStationParameterResponse);

		/// <summary>
		/// 電文のStreamを取得する
		/// <para>各電文の種類に合わせた権限が必要です</para>
		/// <para>StreamはかならずDisposeしてください！</para>
		/// </summary>
		/// <param name="telegramKey">取得する電文のID</param>
		/// <returns>レスポンスのStream</returns>
		public async new Task<Stream> GetTelegramStreamAsync(string telegramKey)
		{
			var url = this.baseUrl == "api.dmdata.jp" ? $"http://data.api.dmdata.jp/v1/{telegramKey}" : $"http://{this.baseUrl}/data/v1/{telegramKey}";

			var apl = AllowPararellRequest;
			if (!apl)
			{
				if (!RequestMre.IsSet && !await Task.Run(() => RequestMre.Wait(Timeout)))
					throw new DmdataApiTimeoutException(url);
				RequestMre.Reset();
			}

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				// サイズのでかいファイルの可能性があるためHeader取得時点で制御を返してもらう
				var response = await Authenticator.ProcessRequestAsync(request, r => HttpClient.SendAsync(r, HttpCompletionOption.ResponseHeadersRead));
				switch (response.StatusCode)
				{
					case System.Net.HttpStatusCode.Forbidden:
						throw new DmdataForbiddenException($"message:{await response.Content.ReadAsStringAsync()} URL: {Authenticator.FilterErrorMessage(url)}");
					case System.Net.HttpStatusCode.PaymentRequired:
						throw new DmdataNotValidContractException("message:{await response.Content.ReadAsStringAsync()} URL: " + Authenticator.FilterErrorMessage(url));
					case System.Net.HttpStatusCode.Unauthorized:
						throw new DmdataUnauthorizedException($"message:{await response.Content.ReadAsStringAsync()} URL: {Authenticator.FilterErrorMessage(url)}");
#if !NET5_0 && !NET6_0
					case (System.Net.HttpStatusCode)429:
#else
					case System.Net.HttpStatusCode.TooManyRequests:
#endif
						throw new DmdataRateLimitExceededException(response.Headers.TryGetValues("Retry-After", out var retry) ? retry.FirstOrDefault() : null);
					case System.Net.HttpStatusCode s when ((int)s / 100) == 5:
						throw new DmdataException("サーバーエラーが発生しています。 StatusCode: " + response.StatusCode);
				}
				if (!response.IsSuccessStatusCode)
					throw new DmdataException("ステータスコードが不正です: " + response.StatusCode);

				return await response.Content.ReadAsStreamAsync();
			}
			catch (TaskCanceledException)
			{
				throw new DmdataApiTimeoutException(Authenticator.FilterErrorMessage(url));
			}
			finally
			{
				if (!apl)
					RequestMre.Set();
			}
		}
	}
}
