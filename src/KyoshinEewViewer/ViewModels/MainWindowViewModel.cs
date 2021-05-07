using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace KyoshinEewViewer.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		[Reactive]
		public string Title { get; set; } = "KyoshinEewViewer for ingen";
		[Reactive]
		public string Version { get; set; } = (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "不明") + "-ALPHA1";

		[Reactive]
		public double Scale { get; set; } = 1;

		public ObservableCollection<SeriesBase> Series { get; } = new ObservableCollection<SeriesBase>();

		[Reactive]
		public Thickness MapPadding { get; set; } = BasePadding;
		private static Thickness BasePadding { get; } = new(0, 36, 0, 0);
		private IDisposable? MapPaddingListener { get; set; }

		[Reactive]
		public IRenderObject[]? RenderObjects { get; protected set; }
		private IDisposable? RenderObjectsListener { get; set; }
		[Reactive]
		public RealtimeRenderObject[]? RealtimeRenderObjects { get; protected set; }
		private IDisposable? RealtimeRenderObjectsListener { get; set; }

		private IDisposable? FocusPointListener { get; set; }

		private SeriesBase? _selectedSeries;
		public SeriesBase? SelectedSeries
		{
			get => _selectedSeries;
			set
			{
				if (_selectedSeries == value)
					return;
				// デタッチ
				MapPaddingListener?.Dispose();
				MapPaddingListener = null;
				RenderObjectsListener?.Dispose();
				RenderObjectsListener = null;
				RealtimeRenderObjectsListener?.Dispose();
				RealtimeRenderObjectsListener = null;
				FocusPointListener?.Dispose();
				FocusPointListener = null;
				_selectedSeries?.Deactivated();

				value?.Activating();
				this.RaiseAndSetIfChanged(ref _selectedSeries, value);

				// アタッチ
				if (_selectedSeries != null)
				{
					MapPaddingListener = _selectedSeries.WhenAnyValue(x => x.MapPadding).Subscribe(x => MapPadding = x + BasePadding);
					MapPadding = _selectedSeries.MapPadding + BasePadding;

					RenderObjectsListener = _selectedSeries.WhenAnyValue(x => x.RenderObjects).Subscribe(x => RenderObjects = x);
					RenderObjects = _selectedSeries.RenderObjects;

					RealtimeRenderObjectsListener = _selectedSeries.WhenAnyValue(x => x.RealtimeRenderObjects).Subscribe(x => RealtimeRenderObjects = x);
					RealtimeRenderObjects = _selectedSeries.RealtimeRenderObjects;

					FocusPointListener = _selectedSeries.WhenAnyValue(x => x.FocusBound).Subscribe(x
						=> MessageBus.Current.SendMessage(new MapNavigationRequested(x)));
					MessageBus.Current.SendMessage(new MapNavigationRequested(_selectedSeries.FocusBound));
				}
				DisplayControl = _selectedSeries?.DisplayControl;
			}
		}
		[Reactive]
		public Control? DisplayControl { get; set; }

		[Reactive]
		public bool UpdateAvailable { get; set; }

		public MainWindowViewModel()
		{
			ConfigurationService.Default.WhenAnyValue(x => x.WindowScale)
				.Subscribe(x => Scale = x);

			Series.Add(new KyoshinMonitorSeries());
			Series.Add(new EarthquakeSeries());

			if (Design.IsDesignMode)
			{
				UpdateAvailable = true;
				return;
			}

			MessageBus.Current.Listen<UpdateFound>().Subscribe(x => UpdateAvailable = x.FoundUpdate?.Any() ?? false);
			UpdateCheckService.Default.StartUpdateCheckTask();
		}

		public void RequestNavigate(Rect rect)
			=> MessageBus.Current.SendMessage(new MapNavigationRequested(rect));
	}
}
