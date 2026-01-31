#pragma warning disable CS0067

using Nina.ManualFocuser.Settings;
using Nina.ManualFocuser.ViewModels;
using Nina.ManualFocuser.Views;
using NINA.Equipment.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Nina.ManualFocuser.Dockables
{
    // ✅ NINA가 MEF로 발견할 수 있게 Export
    [Export(typeof(IDockableVM))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ManualFocuserDockable : IDockableVM, INotifyPropertyChanged
    {
        private readonly ManualFocuserPanelVM _panelVm;

        // IDockableVM 필수
        public string Title { get; set; } = "Manual Focuser Input";
        public string ContentId { get; set; } = "Nina.ManualFocuser.Dockable";

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChanged();

                if (_isVisible)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(EnsureViewOnUIThread));
                }
            }
        }

        public bool IsClosed { get; set; } = false;
        public bool IsTool { get; set; } = true;
        public bool HasSettings { get; set; } = false;
        public bool CanClose { get; set; } = true;
        public bool AutoOpenDockOnce { get; set; } = true;
        public GeometryGroup ImageGeometry { get; set; } = CreateFrozenGeometry();

        private static GeometryGroup CreateFrozenGeometry()
        {
            var g = new GeometryGroup(); 

            g.Children.Add(Geometry.Parse("M 2,2 L 6,2 L 6,3 L 2,3 Z"));
            g.Children.Add(Geometry.Parse("M 2,2 L 3,2 L 3,6 L 2,6 Z"));
            g.Children.Add(Geometry.Parse("M 10,2 L 14,2 L 14,3 L 10,3 Z"));
            g.Children.Add(Geometry.Parse("M 13,2 L 14,2 L 14,6 L 13,6 Z"));
            g.Children.Add(Geometry.Parse("M 2,10 L 3,10 L 3,14 L 2,14 Z"));
            g.Children.Add(Geometry.Parse("M 2,13 L 6,13 L 6,14 L 2,14 Z"));
            g.Children.Add(Geometry.Parse("M 13,10 L 14,10 L 14,14 L 13,14 Z"));
            g.Children.Add(Geometry.Parse("M 10,13 L 14,13 L 14,14 L 10,14 Z"));
            g.Children.Add(Geometry.Parse("M 8,5.0 L 9.0,7.0 L 11.2,7.0 L 9.4,8.3 L 10.1,10.4 L 8,9.2 L 5.9,10.4 L 6.6,8.3 L 4.8,7.0 L 7.0,7.0 Z"));
            g.Freeze();
            return g;
        }


        private void TryAutoOpenOnce()
        {
                    // settings store는 이미 프로젝트에 있으니, 거기서 bool 하나만 저장한다고 가정
            var settings = ManualFocuserSettingsStore.Load();
            if (!settings.AutoOpenDockOnce) return;

            settings.AutoOpenDockOnce = false;
            ManualFocuserSettingsStore.Save(settings);

                    // UI 스레드에서 "열기"를 요청
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                EnsureViewOnUIThread();

                IsClosed = false;
                IsVisible = true;

                OnPropertyChanged(nameof(IsClosed));
                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(Content));
            }));
        }

        public ICommand HideCommand { get; set; }
        public ICommand ToggleSettingsCommand { get; set; }

        // ✅ AvalonDock가 띄울 실제 콘텐츠 (View)
        public object? Content { get; private set; }

        // ✅ XAML에서 PanelVM 바인딩을 유지하려고 노출
        public ManualFocuserPanelVM PanelVM => _panelVm;

        [ImportingConstructor]
        public ManualFocuserDockable(ManualFocuserPanelVM panelVm)
        {
            // ⚠️ 생성자에서는 WPF UIElement를 만들지 않습니다.
            _panelVm = panelVm;

            HideCommand = new SimpleDockCommand(_ => ToggleVisibility());
            ToggleSettingsCommand = new SimpleDockCommand(_ => { /* no settings */ });
            // 핵심: 초기 IsVisible=true면 Content도 미리 준비
            if (IsVisible)
                Application.Current?.Dispatcher.BeginInvoke(new Action(EnsureViewOnUIThread));
            TryAutoOpenOnce();
        }

        public void Hide(object? _)
        {
            IsVisible = false;
            OnPropertyChanged(nameof(IsVisible));
        }

        public void ToggleSettings(object? _)
        {
            // HasSettings=false 이므로 비워둠
        }

        private void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            OnPropertyChanged(nameof(IsVisible));

            if (IsVisible)
                EnsureViewOnUIThread();
        }

        private void EnsureViewOnUIThread()
        {
            if (Content != null) return;

            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Content != null) return;

                var view = new ManualFocuserPanelView
                {
                    // ✅ View의 DataContext는 Dockable (PanelVM 바인딩 유지)
                    DataContext = this
                };

                Content = view;
                OnPropertyChanged(nameof(Content));
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // 간단한 Dock 커맨드
    internal sealed class SimpleDockCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public SimpleDockCommand(Action<object?> execute) => _execute = execute;

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged; // 경고 무시 OK
    }
}