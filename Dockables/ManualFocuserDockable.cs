using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NINA.Equipment.Interfaces.ViewModel;
using Nina.ManualFocuser.ViewModels;
using Nina.ManualFocuser.Views;

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

        public bool IsVisible { get; set; } = true;
        public bool IsClosed { get; set; } = false;
        public bool IsTool { get; set; } = true;
        public bool HasSettings { get; set; } = false;
        public bool CanClose { get; set; } = true;

        public GeometryGroup ImageGeometry { get; set; } = CreateFrozenGeometry();

        private static GeometryGroup CreateFrozenGeometry()
        {
            // 간단한 "포커서" 느낌 아이콘 (원형 + 축)
            var g = new GeometryGroup();

            // 원(렌즈)
            g.Children.Add(Geometry.Parse("M 8,0 A 8,8 0 1 1 7.999,0 Z"));
            // 가운데 점
            g.Children.Add(Geometry.Parse("M 9,8 A 1,1 0 1 1 8.999,8 Z"));
            // 아래 축(막대)
            g.Children.Add(Geometry.Parse("M 7,16 L 9,16 L 9,24 L 7,24 Z"));
            // 좌우 작은 핸들
            g.Children.Add(Geometry.Parse("M 2,18 L 6,18 L 6,20 L 2,20 Z"));
            g.Children.Add(Geometry.Parse("M 10,18 L 14,18 L 14,20 L 10,20 Z"));

            g.Freeze();
            return g;
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