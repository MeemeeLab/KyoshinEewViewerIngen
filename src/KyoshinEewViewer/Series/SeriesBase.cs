﻿using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Map;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace KyoshinEewViewer.Series
{
	public abstract class SeriesBase : ReactiveObject
	{
		protected SeriesBase(string name)
		{
			Name = name;
		}

		public string Name { get; }
		[Reactive]
		public bool IsEnabled { get; protected set; }

		[Reactive]
		public Thickness MapPadding { get; protected set; }
		[Reactive]
		public IRenderObject[]? RenderObjects { get; protected set; }
		[Reactive]
		public RealtimeRenderObject[]? RealtimeRenderObjects { get; protected set; }

		[Reactive]
		public Rect? FocusBound { get; set; }

		public abstract Control DisplayControl { get; }

		public abstract void Activating();
		public abstract void Deactivated();
	}
}