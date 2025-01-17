using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Updater;

public partial class MainWindow : Window
{
	private HttpClient Client { get; } = new();
	private const string GithubReleasesUrl = "https://api.github.com/repos/ingen084/KyoshinEewViewerIngen/releases";
	private string UpdateDirectory { get; set; } = "../";
	private const string SettingsFileName = "config.json";

	// RIDとファイルを紐付ける
	private static Dictionary<string, string> RiMap { get; } = new()
	{
		{ "win10-x64", "KyoshinEewViewer-windows-latest.zip" },
		{ "linux-x64", "KyoshinEewViewer-ubuntu-latest.zip" },
	};

	public MainWindow()
	{
		InitializeComponent();

		Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "KEViUpdater;" + Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
		closeButton.Tapped += (s, e) => Close();
		DoUpdate();
	}

	private async void DoUpdate()
	{
		progress.Value = 0;
		progress.IsIndeterminate = true;
		progressText.Text = "";

		if (Design.IsDesignMode)
			return;

		// 引数で指定されていたら上書きする
		UpdateDirectory = Program.OverrideKevPath ?? UpdateDirectory;

		IDisposable? sentry = null;
		try
		{
			while (Process.GetProcessesByName("KyoshinEewViewer").Any())
			{
				closeButton.IsEnabled = true;
				infoText.Text = "KyoshinEewViewer のプロセスが終了するのを待っています";
				await Task.Delay(1000);
			}
			closeButton.IsEnabled = false;
			infoText.Text = "適用可能な更新を取得しています";

			// アプリによる保存を待ってから
			await Task.Delay(1000);
			if (!File.Exists(Path.Combine("../", SettingsFileName)))
				throw new Exception("KyoshinEewViewerが見つかりません");
			if (JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(Path.Combine("../", SettingsFileName))) is not KyoshinEewViewerConfiguration config)
				throw new Exception("KyoshinEewViewerの設定ファイルを読み込むことができません");

			// 取得してでかい順に並べる
			var version = (await GitHubRelease.GetReleasesAsync(Client, GithubReleasesUrl))
				// ドラフトリリースではなく、現在のバージョンより新しく、不安定版が有効
				.Where(r =>
					!r.Draft &&
					Version.TryParse(r.TagName, out var v) && v > config.SavedVersion &&
					(config.Update.UseUnstableBuild || v.Build == 0))
				.OrderByDescending(r => Version.TryParse(r.TagName, out var v) ? v : new Version())
				.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(version?.Url))
			{
				infoText.Text = "適用可能な更新はありません";
				progress.IsIndeterminate = false;
				closeButton.IsEnabled = true;
				return;
			}

			// エラー情報の収集が有効な場合だけSentryを初期化する
			if (config.Update.SendCrashReport)
			{
				sentry = SentrySdk.Init(o =>
				{
					o.Dsn = "https://5bb942b42f6f4c63ab50a1d429ff69bf@sentry.ingen084.net/3";
					o.AutoSessionTracking = true;
				});
				SentrySdk.ConfigureScope(s =>
				{
					s.Release = Core.Utils.Version;
					s.User = new()
					{
						IpAddress = "{{auto}}",
					};
				});
			}

			infoText.Text = $"v{version.TagName} に更新を行います";

			var ri = RuntimeInformation.RuntimeIdentifier;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				ri = "linux-x64";
			if (!RiMap.ContainsKey(ri))
			{
				infoText.Text = "現在のプラットフォームで自動更新は利用できません";
				progress.IsIndeterminate = false;
				closeButton.IsEnabled = true;
				return;
			}
			var asset = version.Assets.FirstOrDefault(a => a.Name == RiMap[ri]);
			if (asset is null)
			{
				infoText.Text = "リリース内にファイルが見つかりませんでした";
				progress.IsIndeterminate = false;
				closeButton.IsEnabled = true;
				return;
			}

			infoText.Text = $"v{version.TagName} をダウンロードしています";
			progress.IsIndeterminate = false;

			var tmpFileName = Path.GetTempFileName();
			// ダウンロード開始
			using (var fileStream = File.OpenWrite(tmpFileName))
			{
				using var response = await Client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
				progress.Maximum = 100;
				var contentLength = response.Content.Headers.ContentLength ?? throw new Exception("DLサイズが取得できません");

				using var inputStream = await response.Content.ReadAsStreamAsync();

				var total = 0;
				var buffer = new byte[1024];
				while (true)
				{
					var readed = await inputStream.ReadAsync(buffer);
					if (readed == 0)
						break;

					total += readed;
					progress.Value = ((double)total / contentLength) * 100;
					progressText.Text = $"ダウンロード中: {total / 1024:#,0}kb / {contentLength / 1024:#,0}kb";

					await fileStream.WriteAsync(buffer.AsMemory(0, readed));
				}
			}

			infoText.Text = "ファイルを展開しています";
			progress.IsIndeterminate = true;
			progressText.Text = "";

			await Task.Run(() => ZipFile.ExtractToDirectory(tmpFileName, UpdateDirectory, true));
			File.Delete(tmpFileName);
#if LINUX
			new Mono.Unix.UnixFileInfo(Path.Combine(UpdateDirectory, "KyoshinEewViewer")).FileAccessPermissions |=
					Mono.Unix.FileAccessPermissions.UserExecute | Mono.Unix.FileAccessPermissions.GroupExecute | Mono.Unix.FileAccessPermissions.OtherExecute;
#endif

			infoText.Text = "更新が完了しました アプリケーションを起動しています";
			progress.IsIndeterminate = false;

			await Task.Delay(100);

			await Task.Run(() => Process.Start(new ProcessStartInfo(Path.Combine(UpdateDirectory, "KyoshinEewViewer")) { WorkingDirectory = UpdateDirectory }));

			await Task.Delay(2000);

			Close();
		}
		catch (Exception ex)
		{
			SentrySdk.CaptureException(ex);
			infoText.Text = "更新中に問題が発生しました";
			progressText.Text = ex.Message;
			progress.IsIndeterminate = false;
			closeButton.IsEnabled = true;
		}
		finally
		{
			sentry?.Dispose();
		}
	}
}
