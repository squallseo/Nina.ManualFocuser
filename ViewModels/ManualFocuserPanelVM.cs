using Nina.ManualFocuser.Settings;
using NINA.Equipment.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.Mediator; // IFocuserMediator 네임스페이스가 다르면 여기만 수정
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nina.ManualFocuser.ViewModels
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ManualFocuserPanelVM : INotifyPropertyChanged
    {
        private readonly IFocuserMediator _focuserMediator;

        public enum FocuserAction
        {
            None,
            MoveIn,
            MoveOut,
            GoTo
        }

        private FocuserAction _activeAction = FocuserAction.None;
        public FocuserAction ActiveAction
        {
            get => _activeAction;
            private set
            {
                if (_activeAction == value) return;
                _activeAction = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsMoveInActive));
                OnPropertyChanged(nameof(IsMoveOutActive));
                OnPropertyChanged(nameof(IsGoToActive));
            }
        }
        public ICommand StopCommand { get; }

        private async Task StopSafeAsync()
        {
            try
            {
                SetStatus("Stopping…");

                // 3.2.0.9001에서 흔히 제공되는 Stop/Abort 계열을 안전하게 호출
                await InvokeStopAsync();

                SetStatus("Stopped");
            }
            catch (Exception ex)
            {
                SetStatus($"Stop error: {ex.Message}");
            }
            finally
            {
                ActiveAction = FocuserAction.None;
                RefreshStateSafeUI();
            }
        }

        public bool IsBusy => ActiveAction != FocuserAction.None;

        public bool IsMoveInActive => ActiveAction == FocuserAction.MoveIn;
        public bool IsMoveOutActive => ActiveAction == FocuserAction.MoveOut;
        public bool IsGoToActive => ActiveAction == FocuserAction.GoTo;


        private int _step;
        private bool _isConnected;
        private bool _isMoving;
        private string _status = "";
        private int? _position;
        public int? Position
        {
            get => _position;
            private set { if (_position == value) return; _position = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); }
        }

        public string PositionText => Position.HasValue ? Position.Value.ToString() : "-";

        private double? _temperature;
        public double? Temperature
        {
            get => _temperature;
            private set { if (_temperature == value) return; _temperature = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemperatureText)); }
        }

        public string TemperatureText => Temperature.HasValue ? $"{Temperature.Value:0.00} °C" : "-";

        private int _targetPosition;
        public int TargetPosition
        {
            get => _targetPosition;
            set { if (_targetPosition == value) return; _targetPosition = value; OnPropertyChanged(); }
        }

        public ICommand GoToCommand { get; }

        public int Step
        {
            get => _step;
            set
            {
                if (_step == value) return;
                _step = value;
                OnPropertyChanged();
                SaveStep();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public bool IsMoving
        {
            get => _isMoving;
            private set
            {
                if (_isMoving == value) return;
                _isMoving = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            private set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        public string NotConnectedMessage { get; } =
        "Focuser not connected. Connect a focuser in Equipment \u2192 Focuser.";

        public ICommand MoveInCommand { get; }
        public ICommand MoveOutCommand { get; }

        [ImportingConstructor]
        public ManualFocuserPanelVM(IFocuserMediator focuserMediator)
        {
            _focuserMediator = focuserMediator;

            // 1) Step 로드 (기본 100)
            var settings = ManualFocuserSettingsStore.Load();
            Step = settings.Step <= 0 ? 100 : settings.Step;

            // 3) Commands
            MoveInCommand = new SimpleCommand(async _ => await MoveRelativeSafeAsync(-Math.Abs(Step)));
            MoveOutCommand = new SimpleCommand(async _ => await MoveRelativeSafeAsync(+Math.Abs(Step)));
            GoToCommand = new SimpleCommand(async _ => await MoveAbsoluteSafeAsync(TargetPosition));
            StopCommand = new SimpleCommand(async _ => await StopSafeAsync());
            TargetPosition = 1000;
            InitOnUiThread();
        }
        private DispatcherTimer? _pollTimer;

        private void InitOnUiThread()
        {
            TryAddConsumer();
            var app = Application.Current;
            if (app?.Dispatcher == null)
            {
                // NINA 초기화 타이밍에 Application.Current가 없을 수도 있으니, 안전장치
                // 이 경우에는 타이머를 만들지 않고, 버튼 누를 때 Refresh로만 동작
                SetStatus("UI Dispatcher not ready");
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                // UI 스레드에서만 타이머/Refresh 수행
                RefreshStateSafeUI();

                _pollTimer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _pollTimer.Tick += (_, __) => RefreshStateSafeUI();
                _pollTimer.Start();
            }));
        }

        private void SaveStep()
        {
            try
            {
                ManualFocuserSettingsStore.Save(new ManualFocuserSettings { Step = Step });
            }
            catch { /* ignore */ }
        }
        private Task InvokeStopAsync()
        {
            var m = _focuserMediator;
            if (m == null) return Task.CompletedTask;

            // 우선순위: Stop -> Abort -> Halt 등 (존재하는 것만 호출)
            var type = m.GetType();

            object? result;

            var stop = type.GetMethod("Stop");
            if (stop != null)
            {
                result = stop.Invoke(m, null);
                return result as Task ?? Task.CompletedTask;
            }

            var abort = type.GetMethod("Abort");
            if (abort != null)
            {
                result = abort.Invoke(m, null);
                return result as Task ?? Task.CompletedTask;
            }

            var halt = type.GetMethod("Halt");
            if (halt != null)
            {
                result = halt.Invoke(m, null);
                return result as Task ?? Task.CompletedTask;
            }

            SetStatus("Stop not supported");
            return Task.CompletedTask;
        }

        private void RefreshStateSafeUI()
        {
            try
            {
                var device = TryGetDeviceViaGetDevice(_focuserMediator);
                IsConnected = device != null;
                var movingFromDevice = device != null && (TryReadBoolProperty(device, "IsMoving", "Moving", "IsBusy") == true);
                var movingFromMediator = TryReadBoolProperty(_focuserMediator, "IsMoving", "Moving", "IsBusy") == true;

                IsMoving = movingFromDevice || movingFromMediator;

                // Position
                Position = device != null ? TryReadIntProperty(device, "Position", "CurrentPosition") : null;

                // Temperature (지원하는 장비만)
                Temperature = device != null ? TryReadDoubleProperty(device, "Temperature", "Temp", "CurrentTemperature") : null;

                if (IsConnected && Position.HasValue && TargetPosition == 0)
                    TargetPosition = Position.Value;

                if (!IsConnected)
                    SetStatus("");
            }
            catch (Exception ex)
            {
                SetStatus($"Poll error: {ex.GetType().Name}");
            }
        }
        private void RefreshStateRequest()
        {
            var app = Application.Current;
            if (app?.Dispatcher == null)
                return;

            // UI 스레드로 보내서 안전하게 실행
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshStateSafeUI();
            }));
        }
        private static int? TryReadIntProperty(object o, params string[] names)
        {
            var t = o.GetType();
            foreach (var name in names)
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p == null) continue;

                try
                {
                    var v = p.GetValue(o);

                    if (v is int i)
                        return i;

                    var ni = v as int?;
                    if (ni.HasValue)
                        return ni.Value;
                }
                catch
                {
                    // ignore
                }
            }
            return null;
        }

        private async Task MoveAbsoluteSafeAsync(int target)
        {
            try
            {
                var device = TryGetDeviceViaGetDevice(_focuserMediator);
                if (device == null)
                {
                    SetStatus("Not connected");
                    return;
                }

                SetStatus($"Moving to {target}…");

                await InvokeMoveAbsoluteAsync(target);

                SetStatus("Done");
            }
            catch (MissingMethodException ex)
            {
                SetStatus(ex.Message);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                RefreshStateRequest();
            }
        }
        private async Task InvokeMoveAbsoluteAsync(int target)
        {
            var t = _focuserMediator.GetType();

            // NINA에서 흔히 보이는 이름 후보
            // MoveFocuser(int) 가 "절대"일 수도 있어서 우선순위를 아래처럼 둡니다.
            var candidates = new[]
 {
 "MoveFocuserTo",
 "MoveTo",
 "MoveAbsolute",
 "MoveFocuser" // 마지막 fallback
    };

            MethodInfo? method = null;

            foreach (var name in candidates)
            {
                method = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m =>
                {
                    if (m.Name != name) return false;
                    var ps = m.GetParameters();
                    return ps.Length >= 1 && ps[0].ParameterType == typeof(int);
                });

                if (method != null) break;
            }

            if (method == null)
            {
                var available = string.Join(", ",
                t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(x => x.Name)
                .Distinct()
                .OrderBy(x => x)
                .Where(x => x.Contains("Move", StringComparison.OrdinalIgnoreCase)));

                throw new MissingMethodException($"No absolute move method found. Available: {available}");
            }

            var args = BuildArgs(method, target);
            var r = method.Invoke(_focuserMediator, args);
            if (r is Task task) await task;
        }

        private static double? TryReadDoubleProperty(object o, params string[] names)
        {
            var t = o.GetType();
            foreach (var name in names)
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p == null) continue;

                try
                {
                    var v = p.GetValue(o);

                    if (v is double d)
                        return d;

                    if (v is float f)
                        return f;

                    var nd = v as double?;
                    if (nd.HasValue)
                        return nd.Value;

                    var nf = v as float?;
                    if (nf.HasValue)
                        return nf.Value;
                }
                catch
                {
                    // ignore
                }
            }
            return null;
        }


        private async Task MoveRelativeSafeAsync(int steps)
        {
            try
            {
                var device = TryGetDeviceViaGetDevice(_focuserMediator);
                if (device == null)
                {
                    SetStatus("Not connected");
                    return;
                }

                SetStatus($"Moving {(steps < 0 ? "In" : "Out")} ({Math.Abs(steps)})…");

                await InvokeMoveRelativeAsync(steps);

                SetStatus("Done");
            }
            catch (MissingMethodException ex)
            {
                SetStatus(ex.Message);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                ActiveAction = FocuserAction.None;
                RefreshStateRequest();
            }
        }
        private void SetStatus(string text)
        {
            var app = Application.Current;
            if (app?.Dispatcher == null)
            {
                Status = text;
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(() => Status = text));
        }

        // ✅ NINA 3.2에서 보이는 메서드들 기준:
        // MoveFocuserRelative(int), MoveFocuser(int) 등이 있을 수 있어 리플렉션으로 흡수
        private async Task InvokeMoveRelativeAsync(int steps)
        {
            var t = _focuserMediator.GetType();

            // 1) MoveFocuserRelative(...) 우선 탐색 (파라미터 여러개 가능)
            var moveRel = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
 .FirstOrDefault(m =>
 {
     if (m.Name != "MoveFocuserRelative") return false;
     var ps = m.GetParameters();
     return ps.Length >= 1 && ps[0].ParameterType == typeof(int);
 });

            if (moveRel != null)
            {
                var args = BuildArgs(moveRel, steps);
                var r = moveRel.Invoke(_focuserMediator, args);
                if (r is Task task) { await task; return; }
                return;
            }

            // 2) MoveFocuser(...)도 같은 방식으로 (혹시 상대이동으로 쓰는 구현 대비)
            var move = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
 .FirstOrDefault(m =>
 {
     if (m.Name != "MoveFocuser") return false;
     var ps = m.GetParameters();
     return ps.Length >= 1 && ps[0].ParameterType == typeof(int);
 });

            if (move != null)
            {
                var args = BuildArgs(move, steps);
                var r = move.Invoke(_focuserMediator, args);
                if (r is Task task) { await task; return; }
                return;
            }

            // 3) 그래도 없으면 API mismatch
            var available = string.Join(", ",
 t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
 .Select(x => x.Name)
 .Distinct()
 .OrderBy(x => x));

            throw new MissingMethodException($"Move method not found (API mismatch). Available: {available}");
        }

        private static object?[] BuildArgs(MethodInfo m, int steps)
        {
            var ps = m.GetParameters();
            var args = new object?[ps.Length];

            // 첫 번째는 steps
            args[0] = steps;

            // 나머지는 타입 기반으로 기본값 채우기
            for (int i = 1; i < ps.Length; i++)
            {
                var pt = ps[i].ParameterType;

                // CancellationToken
                if (pt.FullName == "System.Threading.CancellationToken")
                {
                    args[i] = default(System.Threading.CancellationToken);
                    continue;
                }

                // bool / nullable bool
                if (pt == typeof(bool)) { args[i] = false; continue; }
                if (pt == typeof(bool?)) { args[i] = (bool?)false; continue; }

                // int / nullable int
                if (pt == typeof(int)) { args[i] = 0; continue; }
                if (pt == typeof(int?)) { args[i] = (int?)0; continue; }

                // enum이면 0
                if (pt.IsEnum) { args[i] = Enum.ToObject(pt, 0); continue; }

                // reference type이면 null
                args[i] = null;
            }

            return args;
        }

        private void TryAddConsumer()
        {
            try
            {
                var t = _focuserMediator.GetType();
                var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public);

                var add = methods.FirstOrDefault(m => string.Equals(m.Name, "AddConsumer", StringComparison.OrdinalIgnoreCase));
                if (add == null) return;

                var ps = add.GetParameters();
                if (ps.Length == 0)
                {
                    add.Invoke(_focuserMediator, null);
                    return;
                }

                if (ps.Length == 1)
                {
                    object arg = ps[0].ParameterType == typeof(string) ? "ManualFocuser" : this;
                    add.Invoke(_focuserMediator, new[] { arg });
                    return;
                }
            }
            catch
            {
                // ignore
            }
        }

        // ✅ GetDevice()로 실제 디바이스 객체 획득 (연결/해제의 근거)
        private static object? TryGetDeviceViaGetDevice(object mediator)
        {
            try
            {
                var t = mediator.GetType();
                var mi = t.GetMethod("GetDevice", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (mi == null) return null;
                return mi.Invoke(mediator, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool? TryReadBoolProperty(object o, params string[] names)
        {
            var t = o.GetType();
            foreach (var name in names)
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p?.PropertyType == typeof(bool))
                    return (bool)p.GetValue(o)!;
                if (p?.PropertyType == typeof(bool?))
                    return (bool?)p.GetValue(o);
            }
            return null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal sealed class SimpleCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        public SimpleCommand(Func<object?, Task> executeAsync) => _executeAsync = executeAsync;

        public bool CanExecute(object? parameter) => true;
        public async void Execute(object? parameter) => await _executeAsync(parameter);
        public event EventHandler? CanExecuteChanged; // 경고 무시 OK
    }
}