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

                    // 원(렌즈)
            g.Children.Add(Geometry.Parse("M 8,0 A 8,8 0 1 1 7.999,0 Z"));
                    // 가운데 점
                     g.Children.Add(Geometry.Parse("M 9,8 A 2.1,2.1 0 1 1 8.999,8 Z"));
                    // 아래 축(막대)
                     g.Children.Add(Geometry.Parse("M 7,16 L 9,16 L 9,24 L 7,24 Z"));
                    // 좌우 작은 핸들
                     g.Children.Add(Geometry.Parse("M 2,18 L 6,18 L 6,20 L 2,20 Z"));
            g.Children.Add(Geometry.Parse("M 10,18 L 14,18 L 14,20 L 10,20 Z"));

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