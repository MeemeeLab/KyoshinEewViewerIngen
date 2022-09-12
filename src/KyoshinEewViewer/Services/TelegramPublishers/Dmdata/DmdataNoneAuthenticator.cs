using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DmdataSharp.Authentication {
	/// <summary>
	/// 認証なし (プロキシサーバー等で使用)
	/// </summary>
	public class NoneAuthenticator : Authenticator
	{
		/// <summary>
		/// 初期化
		/// </summary>
		public NoneAuthenticator() {}

		/// <summary>
		/// そのまま返します
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		public override string FilterErrorMessage(string message) => message;

		/// <summary>
		/// そのままリクエストを実行します
		/// </summary>
		/// <param name="request">付与するHttpRequestMessage</param>
		/// <param name="sendAsync">リクエストを送信するFunc</param>
		/// <returns>レスポンス</returns>
		public override Task<HttpResponseMessage> ProcessRequestAsync(HttpRequestMessage request, Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
		    => sendAsync(request);
	}
}
