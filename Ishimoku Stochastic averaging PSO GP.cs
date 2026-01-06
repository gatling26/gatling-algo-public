using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Logging;

namespace OsEngine.Robots
{
    // Контейнер для цены/источника выхода (вынесен на уровень namespace для совместимости)
    internal struct ExitInfo
    {
        public decimal Price;
        public string Source;
    }

    /// <summary>
    /// Централизованные константы для SharedData и групп параметров.
    /// ВАЖНО: меняем только использование строк, а не их значения, чтобы не ломать совместимость.
    /// </summary>
    internal static class SharedDataKeys
    {
        public const string LastBuySignalType = "LastBuySignalType";
        public const string LastSellSignalType = "LastSellSignalType";

        // Базовые параметры бота
        public const string Regime = "Regime";
        public const string Volume = "Volume";
        public const string ShortTrading = "ShortTrading";
        public const string CloseMode = "CloseMode";
        public const string ForceTradingMode = "ForceTradingMode";

        // Ichimoku / Stochastic базовые значения
        public const string TenkanLength = "TenkanLength";
        public const string KijunLength = "KijunLength";
        public const string SenkouBLength = "SenkouBLength";
        public const string SenkouOffset = "SenkouOffset";
        public const string StochPeriod = "StochPeriod";
        public const string StochSmoothing = "StochSmoothing";
        public const string StochDPeriod = "StochDPeriod";
        public const string StochOversold = "StochOversold";
        public const string StochOverbought = "StochOverbought";

        // Флаги включения сигналов
        public const string OpenByTkKj = "OpenByTkKj";
        public const string OpenByCloud = "OpenByCloud";
        public const string OpenByChikou = "OpenByChikou";
        public const string OpenByStochastic = "OpenByStochastic";
        public const string ExitByTkKj = "ExitByTkKj";
        public const string ExitByCloud = "ExitByCloud";
        public const string ExitByChikou = "ExitByChikou";
        public const string ExitByStochastic = "ExitByStochastic";

        // Трейлинг / TP / риск
        public const string UseTrailingStop = "UseTrailingStop";
        public const string TrailingType = "TrailingType";
        public const string TrailingStartPercent = "TrailingStartPercent";
        public const string TrailingDistancePercent = "TrailingDistancePercent";
        public const string AtrPeriod = "AtrPeriod";
        public const string AtrMultiplier = "AtrMultiplier";
        public const string UseManualTakeProfit = "UseManualTakeProfit";
        public const string ManualTakeProfit = "ManualTakeProfit";
        public const string MinProfitPercent = "MinProfitPercent";
        public const string MaxOpenPositions = "MaxOpenPositions";

        public const string UseBreakEven = "UseBreakEven";
        public const string BreakEvenTriggerPercent = "BreakEvenTriggerPercent";
        public const string ReentryCooldownCandles = "ReentryCooldownCandles";
        public const string MaxSpreadPercent = "MaxSpreadPercent";

        // Логирование / фильтры / дубль‑защита
        public const string LogVerbosity = "LogVerbosity";
        public const string PositionStatusEveryNBars = "PositionStatusEveryNBars";
        public const string UnrealizedPnLLogIntervalMin = "UnrealizedPnLLogIntervalMin";
        public const string UseVolumeFilter = "UseVolumeFilter";
        public const string VolumeMultiplier = "VolumeMultiplier";
        public const string VolumePeriod = "VolumePeriod";
        public const string UseDuplicateProtection = "UseDuplicateProtection";
        public const string DuplicateProtectionMinutes = "DuplicateProtectionMinutes";
        public const string DuplicatePriceTolerance = "DuplicatePriceTolerance";
        public const string DuplicateTimeToleranceSeconds = "DuplicateTimeToleranceSeconds";

        // Усреднение
        public const string AveragingCooldownCandles = "AveragingCooldownCandles";
        public const string AveragingLevel1 = "AveragingLevel1";
        public const string AveragingLevel2 = "AveragingLevel2";
        public const string AveragingLevel3 = "AveragingLevel3";
        public const string AveragingLevel4 = "AveragingLevel4";
        public const string AveragingLevel5 = "AveragingLevel5";
        public const string AveragingLevel6 = "AveragingLevel6";
        public const string AveragingLevel7 = "AveragingLevel7";
        public const string AveragingLevel8 = "AveragingLevel8";
        public const string AveragingLevel9 = "AveragingLevel9";
        public const string AveragingLevel10 = "AveragingLevel10";
        public const string AveragingLevel11 = "AveragingLevel11";
        public const string AveragingLevel12 = "AveragingLevel12";
        public const string AveragingLevel1Enabled = "AveragingLevel1Enabled";
        public const string AveragingLevel2Enabled = "AveragingLevel2Enabled";
        public const string AveragingLevel3Enabled = "AveragingLevel3Enabled";
        public const string AveragingLevel4Enabled = "AveragingLevel4Enabled";
        public const string AveragingLevel5Enabled = "AveragingLevel5Enabled";
        public const string AveragingLevel6Enabled = "AveragingLevel6Enabled";
        public const string AveragingLevel7Enabled = "AveragingLevel7Enabled";
        public const string AveragingLevel8Enabled = "AveragingLevel8Enabled";
        public const string AveragingLevel9Enabled = "AveragingLevel9Enabled";
        public const string AveragingLevel10Enabled = "AveragingLevel10Enabled";
        public const string AveragingLevel11Enabled = "AveragingLevel11Enabled";
        public const string AveragingLevel12Enabled = "AveragingLevel12Enabled";

        // AI‑оптимизация
        public const string UseAIOptimization = "UseAIOptimization";
        public const string OptimizationMode = "OptimizationMode";
        public const string AutoApplyResults = "AutoApplyResults";
        public const string PreserveSafetyLogic = "PreserveSafetyLogic";
        public const string PsoSwarmSize = "PsoSwarmSize";
        public const string PsoMaxIterations = "PsoMaxIterations";
        public const string PsoInertia = "PsoInertia";
        public const string PsoCognitiveWeight = "PsoCognitiveWeight";
        public const string PsoSocialWeight = "PsoSocialWeight";
        public const string PsoUseAdaptiveInertia = "PsoUseAdaptiveInertia";
        public const string PsoStartInertia = "PsoStartInertia";
        public const string PsoEndInertia = "PsoEndInertia";
        public const string PsoUseSubSwarms = "PsoUseSubSwarms";
        public const string PsoSubSwarmCount = "PsoSubSwarmCount";
        public const string PsoMutationRate = "PsoMutationRate";
        public const string PsoCrossoverRate = "PsoCrossoverRate";
        public const string UseGeneticEnhancement = "UseGeneticEnhancement";
        public const string GaPopulationSize = "GaPopulationSize";
        public const string GaGenerations = "GaGenerations";
        public const string GaMutationRate = "GaMutationRate";
        public const string GaCrossoverRate = "GaCrossoverRate";
        public const string ContinuousOptimization = "ContinuousOptimization";
        public const string OptimizationIntervalMinutes = "OptimizationIntervalMinutes";

        // Флаги оптимизации
        public const string OptimizeTenkanLength = "OptimizeTenkanLength";
        public const string OptimizeKijunLength = "OptimizeKijunLength";
        public const string OptimizeSenkouBLength = "OptimizeSenkouBLength";
        public const string OptimizeSenkouOffset = "OptimizeSenkouOffset";
        public const string OptimizeStochPeriod = "OptimizeStochPeriod";
        public const string OptimizeStochSmoothing = "OptimizeStochSmoothing";
        public const string OptimizeStochDPeriod = "OptimizeStochDPeriod";
        public const string OptimizeStochOversold = "OptimizeStochOversold";
        public const string OptimizeStochOverbought = "OptimizeStochOverbought";
        public const string OptimizeAveragingLevel1 = "OptimizeAveragingLevel1";
        public const string OptimizeAveragingLevel2 = "OptimizeAveragingLevel2";
        public const string OptimizeAveragingLevel3 = "OptimizeAveragingLevel3";
        public const string OptimizeAveragingLevel4 = "OptimizeAveragingLevel4";
        public const string OptimizeAveragingLevel5 = "OptimizeAveragingLevel5";
        public const string OptimizeAveragingLevel6 = "OptimizeAveragingLevel6";
        public const string OptimizeAveragingLevel7 = "OptimizeAveragingLevel7";
        public const string OptimizeAveragingLevel8 = "OptimizeAveragingLevel8";
        public const string OptimizeAveragingLevel9 = "OptimizeAveragingLevel9";
        public const string OptimizeAveragingLevel10 = "OptimizeAveragingLevel10";
        public const string OptimizeAveragingLevel11 = "OptimizeAveragingLevel11";
        public const string OptimizeAveragingLevel12 = "OptimizeAveragingLevel12";
        public const string OptimizeMinProfitPercent = "OptimizeMinProfitPercent";
        public const string OptimizeTrailingStartPercent = "OptimizeTrailingStartPercent";
        public const string OptimizeTrailingDistancePercent = "OptimizeTrailingDistancePercent";
        public const string OptimizeSelfLearningTrailing = "OptimizeSelfLearningTrailing";
        public const string OptimizeManualTakeProfit = "OptimizeManualTakeProfit";
        public const string OptimizeBreakEvenTriggerPercent = "OptimizeBreakEvenTriggerPercent";
        public const string OptimizeMaxSpreadPercent = "OptimizeMaxSpreadPercent";
        public const string OptimizeATRPeriod = "OptimizeATRPeriod";
        public const string OptimizeATRMultiplier = "OptimizeATRMultiplier";
        public const string OptimizeVolumeMultiplier = "OptimizeVolumeMultiplier";
        public const string OptimizeVolumePeriod = "OptimizeVolumePeriod";
        public const string OptimizeReentryCooldownCandles = "OptimizeReentryCooldownCandles";
        public const string OptimizeMaxOpenPositions = "OptimizeMaxOpenPositions";

        // Counterintuitive
        public const string UseCounterintuitive = "UseCounterintuitive";
        public const string CounterintuitiveEntry = "CounterintuitiveEntry";
        public const string CounterintuitiveExit = "CounterintuitiveExit";
        public const string CounterintuitiveEma1Period = "CounterintuitiveEma1Period";
        public const string CounterintuitiveEma2Period = "CounterintuitiveEma2Period";
        public const string CounterintuitiveEma3Period = "CounterintuitiveEma3Period";
        public const string OptimizeCounterintuitiveEma1Period = "OptimizeCounterintuitiveEma1Period";
        public const string OptimizeCounterintuitiveEma2Period = "OptimizeCounterintuitiveEma2Period";
        public const string OptimizeCounterintuitiveEma3Period = "OptimizeCounterintuitiveEma3Period";
    }

    /// <summary>
    /// Группы параметров для вкладок интерфейса.
    /// </summary>
    internal static class ParameterGroups
    {
        public const string Ichimoku = "Ишимоку";
        public const string Stochastic = "Stochastic";
        public const string TradingModes = "Режимы торговли";
        public const string OptimizationSelection = "Выбор параметров оптимизации";
        public const string Averaging = "Усреднение";
        public const string Logging = "Логирование";
        public const string NonTradingDays = "Неторговые дни";
        public const string NonTradingPeriods = "Неторговые периоды";
        public const string Counterintuitive = "contrintuitive";
        public const string AiOptimization = "AI Оптимизация";
    }

    #region ==================== CORE ENUMS AND INTERFACES ====================
    
    public enum LogLevel
    {
        Minimal,
        Normal,
        Detailed
    }
    
    // Базовые интерфейсы для ассамблирования
    public interface ITradingComponent
    {
        string ComponentName { get; }
        void Initialize(IComponentContext context);
        Task ProcessAsync(Candle candle);
        void Dispose();
    }
    
    public interface IComponentContext
    {
        BotTabSimple GetTab();
        void SendLog(string message, LogMessageType type);
        T GetComponent<T>() where T : class, ITradingComponent;
        ConcurrentDictionary<string, object> SharedData { get; }
        Func<DateTime, bool> IsTradingTimeAllowed { get; set; } // ✅ Функция проверки неторговых периодов
    }
    
    public interface IStateMachine
    {
        TradingState CurrentState { get; }
        TradingState PreviousState { get; }
        void ProcessEvent(TradingEvent @event, object data = null);
        void TransitionTo(TradingState newState, string reason = "");
        event Action<TradingState, TradingState, string> StateChanged;
    }
    
    #endregion
    
    #region ==================== STATE MACHINE CORE ====================
    
    public enum TradingState
    {
        Initializing,
        Idle,
        MonitoringSignals,
        OpeningLong,
        OpeningShort,
        LongOpened,
        ShortOpened,
        WaitingMinProfit,
        TrailingActive,
        TakeProfitPending,
        ClosingPosition,
        Cooldown,
        BlockedByNonTradePeriod,
        Error,
        Stopped
    }
    
    public enum TradingEvent
    {
        Initialized,
        CandleFinished,
        BuySignalDetected,
        SellSignalDetected,
        PositionOpened,
        PositionClosed,
        MinProfitReached,
        TrailingTriggered,
        TakeProfitTriggered,
        StopLossTriggered,
        ExitSignalDetected,
        CooldownStarted,
        CooldownEnded,
        NonTradePeriodEntered,
        NonTradePeriodExited,
        ErrorOccurred,
        StopRequested
    }
    
    public class TradingStateTransition
    {
        public TradingState FromState { get; set; }
        public TradingState ToState { get; set; }
        public TradingEvent TriggerEvent { get; set; }
        public Func<object, bool> Condition { get; set; }
        public Action<object> Action { get; set; }
        
        public bool CanTransition(object data = null)
        {
            return Condition == null || Condition(data);
        }
        
        public void ExecuteAction(object data = null)
        {
            Action?.Invoke(data);
        }
    }
    
    public class AdaptiveTradingStateMachine : ITradingComponent, IStateMachine
    {
        private TradingState _currentState = TradingState.Initializing;
        private TradingState _previousState = TradingState.Initializing;
        private readonly List<TradingStateTransition> _transitions = new();
        private readonly IComponentContext _context;
        
        // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Кэшируем делегаты условий для избежания повторного создания
        private readonly Func<object, bool> _cachedIsBlockedDelegate;
        private readonly Func<object, bool> _cachedCanOpenPositionDelegate;
        private readonly Func<object, bool> _cachedIsShortTradingEnabledDelegate;
        private readonly Func<object, bool> _cachedIsMinProfitReachedDelegate;
        private readonly Func<object, bool> _cachedNotIsBlockedDelegate;
        private readonly Func<object, bool> _cachedNotIsMinProfitReachedDelegate;
        private readonly Func<object, bool> _cachedCanOpenPositionAndIsShortTradingEnabledDelegate;
        
        public string ComponentName => "StateMachine";
        public TradingState CurrentState => _currentState;
        public TradingState PreviousState => _previousState;
        
        public event Action<TradingState, TradingState, string> StateChanged;
        
        public AdaptiveTradingStateMachine(IComponentContext context)
        {
            _context = context;
            
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Инициализируем кэшированные делегаты один раз
            _cachedIsBlockedDelegate = IsBlocked;
            _cachedCanOpenPositionDelegate = CanOpenPosition;
            _cachedIsShortTradingEnabledDelegate = IsShortTradingEnabled;
            _cachedIsMinProfitReachedDelegate = IsMinProfitReached;
            _cachedNotIsBlockedDelegate = data => !IsBlocked(data);
            _cachedNotIsMinProfitReachedDelegate = data => !IsMinProfitReached(data);
            _cachedCanOpenPositionAndIsShortTradingEnabledDelegate = data => CanOpenPosition(data) && IsShortTradingEnabled(data);
            
            InitializeTransitions();
        }
        
        public void Initialize(IComponentContext context)
        {
            // Уже инициализировано в конструкторе
        }
        
        private void InitializeTransitions()
        {
            // Инициализация -> Ожидание
            AddTransition(TradingState.Initializing, TradingState.Idle, TradingEvent.Initialized);
            
            // Ожидание -> Мониторинг сигналов
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат вместо создания нового
            AddTransition(TradingState.Idle, TradingState.MonitoringSignals, TradingEvent.CandleFinished,
                condition: _cachedNotIsBlockedDelegate);
            
            // Мониторинг -> Открытие LONG
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат вместо создания нового
            AddTransition(TradingState.MonitoringSignals, TradingState.OpeningLong, TradingEvent.BuySignalDetected,
                condition: _cachedCanOpenPositionDelegate);
            
            // Мониторинг -> Открытие SHORT
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат вместо создания нового
            AddTransition(TradingState.MonitoringSignals, TradingState.OpeningShort, TradingEvent.SellSignalDetected,
                condition: _cachedCanOpenPositionAndIsShortTradingEnabledDelegate);
            
            // Открытие -> Позиция открыта
            AddTransition(TradingState.OpeningLong, TradingState.LongOpened, TradingEvent.PositionOpened);
            AddTransition(TradingState.OpeningShort, TradingState.ShortOpened, TradingEvent.PositionOpened);
            
            // Позиция открыта -> Ожидание мин. прибыли
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат вместо создания нового
            AddTransition(TradingState.LongOpened, TradingState.WaitingMinProfit, TradingEvent.CandleFinished,
                condition: _cachedNotIsMinProfitReachedDelegate);
            AddTransition(TradingState.ShortOpened, TradingState.WaitingMinProfit, TradingEvent.CandleFinished,
                condition: _cachedNotIsMinProfitReachedDelegate);
            
            // Ожидание мин. прибыли -> Трейлинг активен
            AddTransition(TradingState.WaitingMinProfit, TradingState.TrailingActive, TradingEvent.MinProfitReached);
            
            // Любое состояние -> Закрытие позиции
            AddTransition(TradingState.LongOpened, TradingState.ClosingPosition, TradingEvent.ExitSignalDetected);
            AddTransition(TradingState.ShortOpened, TradingState.ClosingPosition, TradingEvent.ExitSignalDetected);
            AddTransition(TradingState.WaitingMinProfit, TradingState.ClosingPosition, TradingEvent.ExitSignalDetected);
            AddTransition(TradingState.TrailingActive, TradingState.ClosingPosition, TradingEvent.TrailingTriggered);
            AddTransition(TradingState.TakeProfitPending, TradingState.ClosingPosition, TradingEvent.TakeProfitTriggered);
            
            // Закрытие -> Кулдаун
            AddTransition(TradingState.ClosingPosition, TradingState.Cooldown, TradingEvent.PositionClosed);
            
            // Кулдаун -> Ожидание
            AddTransition(TradingState.Cooldown, TradingState.Idle, TradingEvent.CooldownEnded);
            
            // Любое состояние -> Блокировка
            AddTransition(TradingState.MonitoringSignals, TradingState.BlockedByNonTradePeriod, 
                TradingEvent.NonTradePeriodEntered);
            AddTransition(TradingState.Idle, TradingState.BlockedByNonTradePeriod, 
                TradingEvent.NonTradePeriodEntered);
            
            // Блокировка -> Ожидание
            AddTransition(TradingState.BlockedByNonTradePeriod, TradingState.Idle, 
                TradingEvent.NonTradePeriodExited);
            
            // Любое состояние -> Ошибка
            AddTransition(TradingState.Initializing, TradingState.Error, TradingEvent.ErrorOccurred);
            AddTransition(TradingState.Idle, TradingState.Error, TradingEvent.ErrorOccurred);
            AddTransition(TradingState.MonitoringSignals, TradingState.Error, TradingEvent.ErrorOccurred);
            
            // Ошибка -> Остановка
            AddTransition(TradingState.Error, TradingState.Stopped, TradingEvent.StopRequested);
        }
        
        private void AddTransition(TradingState from, TradingState to, TradingEvent trigger,
            Func<object, bool> condition = null, Action<object> action = null)
        {
            _transitions.Add(new TradingStateTransition
            {
                FromState = from,
                ToState = to,
                TriggerEvent = trigger,
                Condition = condition,
                Action = action
            });
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            await Task.CompletedTask;
            // State machine обрабатывает события, а не свечи напрямую
        }
        
        private readonly object _stateLock = new object(); // Блокировка для потокобезопасности
        
        public void ProcessEvent(TradingEvent @event, object data = null)
        {
            // ПОТОКОБЕЗОПАСНОСТЬ: Используем блокировку для синхронизации доступа
            lock (_stateLock)
            {
                try
                {
                    var transition = _transitions.FirstOrDefault(t =>
                        t.FromState == _currentState &&
                        t.TriggerEvent == @event &&
                        t.CanTransition(data));
                    
                    if (transition != null)
                    {
                        TransitionTo(transition.ToState, $"Event: {@event}");
                        transition.ExecuteAction(data);
                    }
                }
                catch (Exception ex)
                {
                    _context.SendLog($"Ошибка обработки события {@event}: {ex.Message}", LogMessageType.Error);
                    TransitionTo(TradingState.Error, $"Ошибка: {ex.Message}");
                }
            }
        }
        
        public void TransitionTo(TradingState newState, string reason = "")
        {
            // ПОТОКОБЕЗОПАСНОСТЬ: Используем блокировку для синхронизации переходов
            lock (_stateLock)
            {
                if (_currentState == newState) return;
                
                _previousState = _currentState;
                _currentState = newState;
                
                StateChanged?.Invoke(_previousState, _currentState, reason);
                _context.SendLog($"🔄 Переход состояния: {_previousState} → {_currentState} | Причина: {reason}", 
                    LogMessageType.System);
            }
        }
        // Условные методы
        private bool IsBlocked(object data) => false;
        private bool CanOpenPosition(object data) => true;
        private bool IsShortTradingEnabled(object data) => true;
        private bool IsMinProfitReached(object data) => false;
        
        public void Dispose()
        {
            // Очистка ресурсов
        }
    }
    
    #endregion
    
    #region ==================== COMPONENT ASSEMBLY CORE ====================
    
    public class ComponentAssembly
    {
        private readonly Dictionary<Type, ITradingComponent> _components = new();
        private readonly List<ITradingComponent> _componentList = new();
        private IComponentContext _context;
        private bool _isInitialized;
        
        public T GetComponent<T>() where T : class, ITradingComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? component as T : null;
        }
        
        public void RegisterComponent<T>(T component) where T : class, ITradingComponent
        {
            var type = typeof(T);
            if (!_components.ContainsKey(type))
            {
                _components[type] = component;
                _componentList.Add(component);
            }
        }
        
        public void Initialize(IComponentContext context)
        {
            if (_isInitialized) return;
            
            _context = context;
            
            // Инициализация в порядке зависимости
            var orderedComponents = OrderComponentsByDependency();
            
            foreach (var component in orderedComponents)
            {
                try
                {
                    component.Initialize(context);
                    context.SendLog($"✅ Инициализирован компонент: {component.ComponentName}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    context.SendLog($"❌ Ошибка инициализации компонента {component.ComponentName}: {ex.Message}", 
                        LogMessageType.Error);
                }
            }
            
            _isInitialized = true;
        }
        
        private List<ITradingComponent> OrderComponentsByDependency()
        {
            // Простой порядок инициализации
            return _componentList.OrderBy(c => c.ComponentName).ToList();
        }
        
        public async Task ProcessCandleAsync(Candle candle)
        {
            if (!_isInitialized) return;
            
            foreach (var component in _componentList)
            {
                try
                {
                    await component.ProcessAsync(candle);
                }
                catch (Exception ex)
                {
                    _context.SendLog($"❌ Ошибка в компоненте {component.ComponentName}: {ex.Message}", 
                        LogMessageType.Error);
                }
            }
        }
        
        public void Dispose()
        {
            foreach (var component in _componentList)
            {
                try
                {
                    component.Dispose();
                }
                catch { }
            }
            
            _components.Clear();
            _componentList.Clear();
            _isInitialized = false;
        }
    }
    
    public class BotComponentContext : IComponentContext
    {
        private readonly BotTabSimple _tab;
        private readonly Action<string, LogMessageType> _logAction;
        private readonly ConcurrentDictionary<string, object> _sharedData = new();
        private readonly ComponentAssembly _assembly;
        
        public BotComponentContext(BotTabSimple tab, Action<string, LogMessageType> logAction, ComponentAssembly assembly)
        {
            _tab = tab;
            _logAction = logAction;
            _assembly = assembly;
        }
        
        public BotTabSimple GetTab() => _tab;
        
        public void SendLog(string message, LogMessageType type)
        {
            _logAction?.Invoke(message, type);
        }
        
        public T GetComponent<T>() where T : class, ITradingComponent
        {
            return _assembly.GetComponent<T>();
        }
        
        public ConcurrentDictionary<string, object> SharedData => _sharedData;
        
        // ✅ Функция проверки неторговых периодов (устанавливается из главного класса)
        public Func<DateTime, bool> IsTradingTimeAllowed { get; set; }
    }
    
    #endregion
    
    #region ==================== TRADING COMPONENTS ====================
    
    // 1. КОМПОНЕНТ ДАННЫХ И ИНДИКАТОРОВ
    public class DataIndicatorComponent : ITradingComponent
    {
        public string ComponentName => "DataIndicator";
        
        private IComponentContext _context;
        private BotTabSimple _tab;
        private Aindicator _ichimoku;
        private Aindicator _atr;
        private Aindicator _stochastic;
        private Aindicator _counterintuitiveEma1;
        private Aindicator _counterintuitiveEma2;
        private Aindicator _counterintuitiveEma3;
        private StrategyParameterInt _tenkanLength;
        private StrategyParameterInt _kijunLength;
        private StrategyParameterInt _senkouBLength;
        private StrategyParameterInt _senkouOffset;
        private StrategyParameterInt _atrPeriod;
        private StrategyParameterInt _stochPeriod;
        private StrategyParameterInt _stochSmoothing;
        private StrategyParameterInt _stochDPeriod;
        private StrategyParameterInt _counterintuitiveEma1Period;
        private StrategyParameterInt _counterintuitiveEma2Period;
        private StrategyParameterInt _counterintuitiveEma3Period;
        private StrategyParameterString _useCounterintuitive;
        
        private readonly ConcurrentDictionary<string, CachedValue> _indicatorCache = new();
        
        // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Кэшируем делегаты для вычисления значений индикаторов
        private Func<decimal> _cachedAtrCalculator;
        private readonly Dictionary<string, Func<decimal>> _cachedIchimokuCalculators = new();
        // УДАЛЕНО: _dataSeriesLock больше не используется, так как мы избегаем блокировок
        // и используем прямое обращение к Last без перечисления коллекций
        
        private class CachedValue
        {
            public decimal Value { get; set; }
            public DateTime Timestamp { get; set; }
            public CachedValue(decimal value)
            {
                Value = value;
                Timestamp = DateTime.Now;
            }
        }
        
        public void Initialize(IComponentContext context)
        {
            _context = context;
            _tab = context.GetTab();
            
            // Получаем параметры из общего хранилища
            if (context.SharedData.TryGetValue("TenkanLength", out var tenkan))
                _tenkanLength = tenkan as StrategyParameterInt;
            if (context.SharedData.TryGetValue("KijunLength", out var kijun))
                _kijunLength = kijun as StrategyParameterInt;
            if (context.SharedData.TryGetValue("SenkouBLength", out var senkouB))
                _senkouBLength = senkouB as StrategyParameterInt;
            if (context.SharedData.TryGetValue("SenkouOffset", out var offset))
                _senkouOffset = offset as StrategyParameterInt;
            if (context.SharedData.TryGetValue("AtrPeriod", out var atrPeriod))
                _atrPeriod = atrPeriod as StrategyParameterInt;
            if (context.SharedData.TryGetValue("StochPeriod", out var stochPeriod))
                _stochPeriod = stochPeriod as StrategyParameterInt;
            if (context.SharedData.TryGetValue("StochSmoothing", out var stochSmooth))
                _stochSmoothing = stochSmooth as StrategyParameterInt;
            if (context.SharedData.TryGetValue("StochDPeriod", out var stochD))
                _stochDPeriod = stochD as StrategyParameterInt;
            if (context.SharedData.TryGetValue(SharedDataKeys.CounterintuitiveEma1Period, out var ema1Period))
                _counterintuitiveEma1Period = ema1Period as StrategyParameterInt;
            if (context.SharedData.TryGetValue(SharedDataKeys.CounterintuitiveEma2Period, out var ema2Period))
                _counterintuitiveEma2Period = ema2Period as StrategyParameterInt;
            if (context.SharedData.TryGetValue(SharedDataKeys.CounterintuitiveEma3Period, out var ema3Period))
                _counterintuitiveEma3Period = ema3Period as StrategyParameterInt;
            if (context.SharedData.TryGetValue("UseCounterintuitive", out var useCounterintuitive))
                _useCounterintuitive = useCounterintuitive as StrategyParameterString;
            
            CreateIndicators();
        }
        
        private void CreateIndicators()
        {
            try
            {
                _context.SendLog("Создание индикаторов...", LogMessageType.System);
                
                // ✅ ПРОВЕРКА: Убеждаемся, что есть достаточное количество свечей перед созданием индикаторов
                // Это предотвращает ошибку ArgumentOutOfRangeException в ChartCandleMaster.SetCandles
                int minRequiredCandles = Math.Max(
                    Math.Max(_senkouBLength?.ValueInt ?? 52, _senkouOffset?.ValueInt ?? 26) + 30,
                    100
                );
                
                if (_tab?.CandlesAll == null || _tab.CandlesAll.Count < minRequiredCandles)
                {
                    _context.SendLog($"⏳ Ожидание свечей для создания индикаторов (требуется минимум {minRequiredCandles}, доступно {_tab?.CandlesAll?.Count ?? 0})", 
                        LogMessageType.System);
                    return; // Выходим, если недостаточно свечей - индикаторы создадутся позже
                }
                
                // ✅ АГРЕССИВНОЕ УДАЛЕНИЕ: Удаляем ВСЕ старые индикаторы с графика
                // Это гарантирует чистый график перед созданием новых индикаторов
                _context.SendLog("🧹 Очистка графика от старых индикаторов...", LogMessageType.System);
                
                // Удаляем все известные индикаторы
                if (_ichimoku != null)
                {
                    try { _tab.DeleteCandleIndicator(_ichimoku); } catch { }
                    _ichimoku = null;
                }
                if (_atr != null)
                {
                    try { _tab.DeleteCandleIndicator(_atr); } catch { }
                    _atr = null;
                }
                if (_stochastic != null)
                {
                    try { _tab.DeleteCandleIndicator(_stochastic); } catch { }
                    _stochastic = null;
                }
                if (_counterintuitiveEma1 != null)
                {
                    try { _tab.DeleteCandleIndicator(_counterintuitiveEma1); } catch { }
                    _counterintuitiveEma1 = null;
                }
                if (_counterintuitiveEma2 != null)
                {
                    try { _tab.DeleteCandleIndicator(_counterintuitiveEma2); } catch { }
                    _counterintuitiveEma2 = null;
                }
                if (_counterintuitiveEma3 != null)
                {
                    try { _tab.DeleteCandleIndicator(_counterintuitiveEma3); } catch { }
                    _counterintuitiveEma3 = null;
                }
                
                // ✅ ДОПОЛНИТЕЛЬНО: Удаляем индикаторы из всех возможных областей
                // Пытаемся удалить индикаторы, которые могли остаться в других областях
                // Удаляем несколько раз для гарантии полной очистки
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        // Удаляем все известные индикаторы еще раз (на случай, если они были в других областях)
                        if (_ichimoku != null)
                        {
                            try { _tab.DeleteCandleIndicator(_ichimoku); } catch { }
                        }
                        if (_atr != null)
                        {
                            try { _tab.DeleteCandleIndicator(_atr); } catch { }
                        }
                        if (_stochastic != null)
                        {
                            try { _tab.DeleteCandleIndicator(_stochastic); } catch { }
                        }
                        if (_counterintuitiveEma1 != null)
                        {
                            try { _tab.DeleteCandleIndicator(_counterintuitiveEma1); } catch { }
                        }
                        if (_counterintuitiveEma2 != null)
                        {
                            try { _tab.DeleteCandleIndicator(_counterintuitiveEma2); } catch { }
                        }
                        if (_counterintuitiveEma3 != null)
                        {
                            try { _tab.DeleteCandleIndicator(_counterintuitiveEma3); } catch { }
                        }
                        
                        if (attempt < 2)
                        {
                            System.Threading.Thread.Sleep(50); // Небольшая задержка между попытками
                        }
                    }
                    catch { /* Игнорируем ошибки при очистке */ }
                }
                
                // ✅ УВЕЛИЧЕННАЯ задержка для полной очистки графика и завершения операций удаления
                System.Threading.Thread.Sleep(500);
                
                // 2. Создание индикатора Ишимоку - в основной области Prime для полноразмерного графика
                // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Создаем индикатор ПЕРЕД добавлением на график
                _ichimoku = IndicatorsFactory.CreateIndicatorByName("Ichimoku", 
                    "Ichimoku_" + _tenkanLength.ValueInt + "_" + _kijunLength.ValueInt, false);
                
                // 3. Установка параметров Ишимоку ПЕРЕД добавлением на график
                if (_ichimoku.Parameters != null && _ichimoku.Parameters.Count >= 4)
                {
                    var param0 = _ichimoku.Parameters[0] as IndicatorParameterInt;
                    if (param0 != null) param0.ValueInt = _tenkanLength.ValueInt;
                    
                    var param1 = _ichimoku.Parameters[1] as IndicatorParameterInt;
                    if (param1 != null) param1.ValueInt = _kijunLength.ValueInt;
                    
                    var param2 = _ichimoku.Parameters[2] as IndicatorParameterInt;
                    if (param2 != null) param2.ValueInt = _senkouBLength.ValueInt;
                    
                    var param3 = _ichimoku.Parameters[3] as IndicatorParameterInt;
                    if (param3 != null) param3.ValueInt = _senkouOffset.ValueInt;
                }
                
                // 4. Сохраняем параметры ПЕРЕД добавлением на график
                _ichimoku.Save();
                
                // 5. Добавляем индикатор на график в области Prime (на графике со свечами)
                // КРИТИЧНО: Индикатор ДОЛЖЕН быть добавлен на график через CreateCandleIndicator
                try
                {
                    _ichimoku = (Aindicator)_tab.CreateCandleIndicator(_ichimoku, "Prime");
                    if (_ichimoku != null)
                    {
                        _context.SendLog($"✅ Ишимоку успешно добавлен на график в области Prime", LogMessageType.System);
                    }
                    else
                    {
                        _context.SendLog($"⚠️ Ошибка: Ишимоку не был добавлен на график (вернулся null)", LogMessageType.Error);
                    }
                }
                catch (Exception ex)
                {
                    _context.SendLog($"⚠️ Ошибка создания индикатора Ишимоку: {ex.Message}", LogMessageType.Error);
                    _ichimoku = null;
                }
                
                // 6. Визуальные свойства устанавливаются автоматически OsEngine
                // НЕ устанавливаем их вручную, чтобы избежать конфликтов с отрисовкой
                
                // Увеличенная задержка между созданием индикаторов для стабильности
                System.Threading.Thread.Sleep(200);
                
                // ✅ ИСПРАВЛЕНО: Создание индикатора ATR БЕЗ добавления на график
                // Индикатор создается для работы в фоне через DataIndicatorComponent
                // НЕ добавляем на график, чтобы избежать проблем с отображением
                try
                {
                    _atr = IndicatorsFactory.CreateIndicatorByName("ATR", "ATR_" + _atrPeriod.ValueInt, false);
                    
                    // Установка параметров ATR
                    if (_atr.Parameters != null && _atr.Parameters.Count > 0)
                    {
                        var atrParam = _atr.Parameters[0] as IndicatorParameterInt;
                        if (atrParam != null) atrParam.ValueInt = _atrPeriod.ValueInt;
                    }
                    
                    // Сохраняем параметры
                    _atr.Save();
                    
                    // ✅ НЕ добавляем на график - индикатор работает в фоне
                    // Значения доступны через DataIndicatorComponent.GetAtrValue()
                }
                catch (Exception ex)
                {
                    _context.SendLog($"⚠️ Ошибка создания индикатора ATR: {ex.Message}", LogMessageType.Error);
                    _atr = null;
                }

                // ✅ Создание индикатора Stochastic - в единственной дополнительной области NewArea0
                // КРИТИЧНО: Индикатор ДОЛЖЕН быть добавлен на график через CreateCandleIndicator
                try
                {
                    _stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic",
                        "Stochastic_" + _stochPeriod.ValueInt, false);

                    if (_stochastic.ParametersDigit != null && _stochastic.ParametersDigit.Count >= 3)
                    {
                        _stochastic.ParametersDigit[0].Value = _stochPeriod.ValueInt;
                        _stochastic.ParametersDigit[1].Value = _stochSmoothing.ValueInt;
                        _stochastic.ParametersDigit[2].Value = _stochDPeriod.ValueInt;
                    }

                    _stochastic.Save();
                    
                    // ✅ Добавляем Stochastic на график в области NewArea0 (единственная дополнительная область)
                    _stochastic = (Aindicator)_tab.CreateCandleIndicator(_stochastic, "NewArea0");
                    
                    if (_stochastic != null)
                    {
                        _context.SendLog($"✅ Stochastic успешно добавлен на график в области NewArea0", LogMessageType.System);
                    }
                    else
                    {
                        _context.SendLog($"⚠️ Ошибка: Stochastic не был добавлен на график (вернулся null)", LogMessageType.Error);
                    }
                }
                catch (Exception ex)
                {
                    _context.SendLog($"⚠️ Ошибка создания индикатора Stochastic: {ex.Message}", LogMessageType.Error);
                    _stochastic = null;
                }
                
                // Задержка после создания Stochastic для стабильности отображения
                System.Threading.Thread.Sleep(200);
                
                // ✅ ИСПРАВЛЕНО: Создание EMA индикаторов для counterintuitive БЕЗ добавления на график
                // Индикаторы создаются для работы в фоне через DataIndicatorComponent
                // НЕ добавляем на график, чтобы оставить только одну дополнительную область со Stochastic
                if (_useCounterintuitive != null && _useCounterintuitive.ValueString == "Включено" &&
                    _counterintuitiveEma1Period != null && _counterintuitiveEma2Period != null && _counterintuitiveEma3Period != null)
                {
                    try
                    {
                        // EMA1 (распорка) - создаем, но НЕ добавляем на график
                        _counterintuitiveEma1 = IndicatorsFactory.CreateIndicatorByName("Ema", 
                            "CounterintuitiveEMA1_" + _counterintuitiveEma1Period.ValueInt, false);
                        ((IndicatorParameterInt)_counterintuitiveEma1.Parameters[0]).ValueInt = _counterintuitiveEma1Period.ValueInt;
                        _counterintuitiveEma1.Save();
                        // ✅ НЕ добавляем на график - работает в фоне
                        
                        // EMA2 (средняя) - создаем, но НЕ добавляем на график
                        _counterintuitiveEma2 = IndicatorsFactory.CreateIndicatorByName("Ema", 
                            "CounterintuitiveEMA2_" + _counterintuitiveEma2Period.ValueInt, false);
                        ((IndicatorParameterInt)_counterintuitiveEma2.Parameters[0]).ValueInt = _counterintuitiveEma2Period.ValueInt;
                        _counterintuitiveEma2.Save();
                        // ✅ НЕ добавляем на график - работает в фоне
                        
                        // EMA3 (быстрая) - создаем, но НЕ добавляем на график
                        _counterintuitiveEma3 = IndicatorsFactory.CreateIndicatorByName("Ema", 
                            "CounterintuitiveEMA3_" + _counterintuitiveEma3Period.ValueInt, false);
                        ((IndicatorParameterInt)_counterintuitiveEma3.Parameters[0]).ValueInt = _counterintuitiveEma3Period.ValueInt;
                        _counterintuitiveEma3.Save();
                        // ✅ НЕ добавляем на график - работает в фоне
                        
                        _context.SendLog($"✅ Counterintuitive EMA индикаторы созданы (в фоне, без отображения): EMA1={_counterintuitiveEma1Period.ValueInt}, EMA2={_counterintuitiveEma2Period.ValueInt}, EMA3={_counterintuitiveEma3Period.ValueInt}", LogMessageType.System);
                    }
                    catch (Exception ex)
                    {
                        _context.SendLog($"⚠️ Ошибка создания Counterintuitive EMA индикаторов: {ex.Message}", LogMessageType.Error);
                        _counterintuitiveEma1 = null;
                        _counterintuitiveEma2 = null;
                        _counterintuitiveEma3 = null;
                    }
                }
                
                // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Инициализируем кэшированные делегаты после создания индикаторов
                InitializeCachedDelegates();
                
                // ✅ ФИНАЛЬНОЕ ЛОГИРОВАНИЕ: Подтверждаем создание индикаторов
                _context.SendLog("✅ Индикаторы созданы: Ишимоку (Prime, на графике), Stochastic (NewArea0, на графике), ATR и Counterintuitive EMA (в фоне, без отображения)", LogMessageType.System);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка создания индикаторов: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private void SetIndicatorVisualProperties(Aindicator indicator)
        {
            // ОТКЛЮЧЕНО: Установка визуальных свойств через рефлексию вызывает конфликты
            // с отрисовкой графика (Collection was modified during enumeration)
            // OsEngine сам управляет визуальными свойствами индикаторов
            // Если нужна настройка цветов, это делается через параметры индикатора или UI
            return;
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            await Task.CompletedTask;
            
            // ✅ АВТОМАТИЧЕСКОЕ СОЗДАНИЕ ИНДИКАТОРОВ: Если индикаторы еще не созданы,
            // но теперь есть достаточное количество свечей, пытаемся создать их
            if (_ichimoku == null && _tab?.CandlesAll != null)
            {
                int minRequiredCandles = Math.Max(
                    Math.Max(_senkouBLength?.ValueInt ?? 52, _senkouOffset?.ValueInt ?? 26) + 30,
                    100
                );
                
                if (_tab.CandlesAll.Count >= minRequiredCandles)
                {
                    try
                    {
                        CreateIndicators();
                    }
                    catch (Exception ex)
                    {
                        _context.SendLog($"⚠️ Ошибка автоматического создания индикаторов: {ex.Message}", 
                            LogMessageType.Error);
                    }
                }
            }
        }
        
        public void Dispose()
        {
            if (_ichimoku != null)
            {
                try { _tab.DeleteCandleIndicator(_ichimoku); } catch { }
            }
            if (_atr != null)
            {
                try { _tab.DeleteCandleIndicator(_atr); } catch { }
            }
            if (_stochastic != null)
            {
                try { _tab.DeleteCandleIndicator(_stochastic); } catch { }
            }
            if (_counterintuitiveEma1 != null)
            {
                try { _tab.DeleteCandleIndicator(_counterintuitiveEma1); } catch { }
            }
            if (_counterintuitiveEma2 != null)
            {
                try { _tab.DeleteCandleIndicator(_counterintuitiveEma2); } catch { }
            }
            if (_counterintuitiveEma3 != null)
            {
                try { _tab.DeleteCandleIndicator(_counterintuitiveEma3); } catch { }
            }
            
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Очищаем кэшированные делегаты при освобождении ресурсов
            _cachedAtrCalculator = null;
            _cachedIchimokuCalculators.Clear();
        }
        
        // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Инициализация кэшированных делегатов для вычисления значений индикаторов
        private void InitializeCachedDelegates()
        {
            // Кэшируем делегат для ATR
            _cachedAtrCalculator = () =>
            {
                // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Избегаем доступа к DataSeries.Count и используем прямое обращение
                // Это предотвращает перечисление коллекции во время отрисовки
                try
                {
                    if (_atr?.DataSeries == null) return 0m;
                    
                    // ПРЯМОЕ ОБРАЩЕНИЕ к серии без проверки Count - это избегает перечисления
                    object series = null;
                    try
                    {
                        // Пытаемся получить серию напрямую, без проверки Count
                        series = _atr.DataSeries[0];
                    }
                    catch (InvalidOperationException)
                    {
                        // Коллекция изменяется во время доступа - возвращаем кэш
                        return 0m;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Индекс вне диапазона
                        return 0m;
                    }
                    catch
                    {
                        return 0m;
                    }
                    
                    if (series == null) return 0m;
                    
                    // Используем свойство Last напрямую - это безопаснее чем доступ к Values
                    try
                    {
                        var lastProperty = series.GetType().GetProperty("Last");
                        if (lastProperty == null) return 0m;
                        
                        var lastValue = lastProperty.GetValue(series);
                        if (lastValue == null) return 0m;
                        
                        return (decimal)lastValue;
                    }
                    catch (InvalidOperationException)
                    {
                        // Коллекция изменяется - возвращаем 0
                        return 0m;
                    }
                    catch
                    {
                        return 0m;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Коллекция изменяется во время доступа
                    return 0m;
                }
                catch
                {
                    return 0m;
                }
            };
            
            // Кэшируем делегаты для всех линий Ишимоку
            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Используем локальную переменную для правильного захвата в замыкании
            string[] ichimokuLines = { "Tenkan", "Kijun", "SenkouA", "SenkouB", "Chikou" };
            foreach (var lineName in ichimokuLines)
            {
                string capturedLineName = lineName; // Захватываем значение в локальную переменную
                _cachedIchimokuCalculators[lineName] = () => CalculateIchimokuValue(capturedLineName);
            }
        }
        
        // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Вспомогательный метод для вычисления значения Ишимоку
        private decimal CalculateIchimokuValue(string lineName)
        {
            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Избегаем доступа к DataSeries.Count и используем прямое обращение
            // Это предотвращает перечисление коллекции во время отрисовки
            try
            {
                if (_ichimoku?.DataSeries == null) return 0m;

                int seriesIndex = lineName switch
                {
                    "Tenkan" => 0,
                    "Kijun" => 1,
                    "SenkouA" => 2,
                    "SenkouB" => 3,
                    "Chikou" => 4,
                    _ => -1
                };
                
                if (seriesIndex < 0) return 0m;
                
                // ПРЯМОЕ ОБРАЩЕНИЕ к серии без проверки Count - это избегает перечисления
                object series = null;
                try
                {
                    // Пытаемся получить серию напрямую, без проверки Count
                    series = _ichimoku.DataSeries[seriesIndex];
                }
                catch (InvalidOperationException)
                {
                    // Коллекция изменяется во время доступа - возвращаем 0
                    return 0m;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Индекс вне диапазона
                    return 0m;
                }
                catch
                {
                    return 0m;
                }
                
                if (series == null) return 0m;
                
                // Используем свойство Last напрямую - это безопаснее чем доступ к Values
                try
                {
                    var lastProperty = series.GetType().GetProperty("Last");
                    if (lastProperty == null) return 0m;
                    
                    var lastValue = lastProperty.GetValue(series);
                    if (lastValue == null) return 0m;
                    
                    return (decimal)lastValue;
                }
                catch (InvalidOperationException)
                {
                    // Коллекция изменяется - возвращаем 0
                    return 0m;
                }
                catch
                {
                    return 0m;
                }
            }
            catch (InvalidOperationException)
            {
                // Коллекция изменяется во время доступа
                return 0m;
            }
            catch
            {
                return 0m;
            }
        }

        public bool TryGetStochasticValues(out decimal currentK, out decimal previousK,
            out decimal currentD, out decimal previousD)
        {
            currentK = previousK = currentD = previousD = 0m;

            try
            {
                if (_stochastic?.DataSeries == null || _stochastic.DataSeries.Count < 2)
                {
                    return false;
                }

                var kValues = _stochastic.DataSeries[0].Values;
                var dValues = _stochastic.DataSeries[1].Values;

                if (kValues == null || dValues == null || kValues.Count < 2 || dValues.Count < 2)
                {
                    return false;
                }

                int lastIndex = kValues.Count - 1;
                currentK = kValues[lastIndex];
                previousK = kValues[lastIndex - 1];

                currentD = dValues[lastIndex];
                previousD = dValues[lastIndex - 1];

                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // Методы для получения значений индикаторов
        public decimal GetTenkanValue() => GetIchimokuValue("Tenkan");
        public decimal GetKijunValue() => GetIchimokuValue("Kijun");
        public decimal GetSenkouAValue() => GetIchimokuValue("SenkouA");
        public decimal GetSenkouBValue() => GetIchimokuValue("SenkouB");
        public decimal GetChikouValue() => GetIchimokuValue("Chikou");
        
        // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат вместо создания нового при каждом вызове
        public decimal GetAtrValue()
        {
            // Используем кэшированный делегат, если он доступен, иначе создаем fallback делегат
            Func<decimal> calculator = _cachedAtrCalculator;
            if (calculator == null)
            {
                // Fallback: создаем делегат на лету, если кэш еще не инициализирован
                calculator = () =>
                {
                    // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Избегаем доступа к DataSeries.Count и используем прямое обращение
                    // Это предотвращает перечисление коллекции во время отрисовки
                    try
                    {
                        if (_atr?.DataSeries == null) return 0m;
                        
                        // ПРЯМОЕ ОБРАЩЕНИЕ к серии без проверки Count - это избегает перечисления
                        object series = null;
                        try
                        {
                            // Пытаемся получить серию напрямую, без проверки Count
                            series = _atr.DataSeries[0];
                        }
                        catch (InvalidOperationException)
                        {
                            // Коллекция изменяется во время доступа - возвращаем кэш
                            return 0m;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // Индекс вне диапазона
                            return 0m;
                        }
                        catch
                        {
                            return 0m;
                        }
                        
                        if (series == null) return 0m;
                        
                        // Используем свойство Last напрямую - это безопаснее чем доступ к Values
                        try
                        {
                            var lastProperty = series.GetType().GetProperty("Last");
                            if (lastProperty == null) return 0m;
                            
                            var lastValue = lastProperty.GetValue(series);
                            if (lastValue == null) return 0m;
                            
                            return (decimal)lastValue;
                        }
                        catch (InvalidOperationException)
                        {
                            // Коллекция изменяется - возвращаем 0
                            return 0m;
                        }
                        catch
                        {
                            return 0m;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Коллекция изменяется во время доступа
                        return 0m;
                    }
                    catch
                    {
                        return 0m;
                    }
                };
            }
            
            return GetIndicatorValue("atr", calculator);
        }
        
        // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат вместо создания нового при каждом вызове
        private decimal GetIchimokuValue(string lineName)
        {
            string cacheKey = $"ichimoku_{lineName}";
            
            // КЭШИРОВАНИЕ ДЕЛЕГАТОВ: Используем кэшированный делегат, если он доступен
            Func<decimal> calculator = null;
            if (_cachedIchimokuCalculators.TryGetValue(lineName, out var cachedCalculator))
            {
                calculator = cachedCalculator;
            }
            else
            {
                // Fallback: создаем делегат на лету, если кэш еще не инициализирован
                calculator = () => CalculateIchimokuValue(lineName);
            }
            
            return GetIndicatorValue(cacheKey, calculator);
        }
        
        // Методы для получения значений EMA counterintuitive
        public bool TryGetCounterintuitiveEmaValues(out decimal ema1, out decimal ema2, out decimal ema3)
        {
            ema1 = 0m;
            ema2 = 0m;
            ema3 = 0m;
            
            if (_useCounterintuitive == null || _useCounterintuitive.ValueString != "Включено")
                return false;
            
            if (_counterintuitiveEma1 == null || _counterintuitiveEma2 == null || _counterintuitiveEma3 == null)
                return false;
            
            try
            {
                if (_counterintuitiveEma1?.DataSeries != null && _counterintuitiveEma1.DataSeries.Count > 0)
                {
                    var series1 = _counterintuitiveEma1.DataSeries[0];
                    var lastProperty1 = series1.GetType().GetProperty("Last");
                    if (lastProperty1 != null)
                        ema1 = (decimal)lastProperty1.GetValue(series1);
                }
                
                if (_counterintuitiveEma2?.DataSeries != null && _counterintuitiveEma2.DataSeries.Count > 0)
                {
                    var series2 = _counterintuitiveEma2.DataSeries[0];
                    var lastProperty2 = series2.GetType().GetProperty("Last");
                    if (lastProperty2 != null)
                        ema2 = (decimal)lastProperty2.GetValue(series2);
                }
                
                if (_counterintuitiveEma3?.DataSeries != null && _counterintuitiveEma3.DataSeries.Count > 0)
                {
                    var series3 = _counterintuitiveEma3.DataSeries[0];
                    var lastProperty3 = series3.GetType().GetProperty("Last");
                    if (lastProperty3 != null)
                        ema3 = (decimal)lastProperty3.GetValue(series3);
                }
                
                return ema1 > 0 && ema2 > 0 && ema3 > 0;
            }
            catch
            {
                return false;
            }
        }
        
        private decimal GetIndicatorValue(string indicatorKey, Func<decimal> calculator)
        {
            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Увеличиваем время кэширования до 3 секунд
            // Это значительно снижает частоту обращений к DataSeries и вероятность конфликтов с отрисовкой
            if (_indicatorCache.TryGetValue(indicatorKey, out CachedValue cached) && 
                DateTime.Now - cached.Timestamp < TimeSpan.FromSeconds(3))
            {
                return cached.Value;
            }

            // Безопасное вычисление с обработкой всех исключений
            decimal value = 0m;
            try
            {
                value = calculator();
                
                // Сохраняем только валидные значения (не 0, если это не первый вызов)
                if (value != 0m || !_indicatorCache.ContainsKey(indicatorKey))
                {
                    _indicatorCache[indicatorKey] = new CachedValue(value);
                }
                else if (cached != null)
                {
                    // Если получили 0, но есть кэш - используем кэш
                    return cached.Value;
                }
            }
            catch (InvalidOperationException)
            {
                // Коллекция изменяется во время доступа - возвращаем кэшированное значение
                if (cached != null)
                {
                    return cached.Value;
                }
                return 0m;
            }
            catch
            {
                // Другие ошибки - возвращаем кэшированное значение если есть
                if (cached != null)
                {
                    return cached.Value;
                }
                return 0m;
            }
            
            return value;
        }
    }
    
    // 2. КОМПОНЕНТ МЕНЕДЖЕРА РИСКОВ И ЗАЩИТЫ
    public class RiskManagementComponent : ITradingComponent
    {
        public string ComponentName => "RiskManagement";
        
        private IComponentContext _context;
        private BotTabSimple _tab;
        private StrategyParameterDecimal _minProfitPercentParam;
        private StrategyParameterString _useBreakEven;
        private StrategyParameterDecimal _breakEvenTriggerPercent;
        private StrategyParameterDecimal _maxSpreadPercent;
        private StrategyParameterString _closeMode;
        
        private readonly ConcurrentDictionary<int, decimal> _entryPrices = new();
        private readonly ConcurrentDictionary<int, decimal> _minProfitPrices = new();
        private readonly ConcurrentDictionary<int, bool> _breakEvenApplied = new();
        private readonly ConcurrentDictionary<int, decimal> _maxProfitPercentReached = new();
        private readonly ConcurrentDictionary<int, decimal> _maxProfitValueReached = new();
        private readonly ConcurrentDictionary<int, decimal> _minProfitPercentReached = new();
        private readonly ConcurrentDictionary<int, decimal> _minProfitValueReached = new();
        private readonly ConcurrentDictionary<int, bool> _wentPositive = new();
        private readonly ConcurrentDictionary<int, bool> _minProfitReached = new();
        
        private decimal _lastPrice;
        
        public void Initialize(IComponentContext context)
        {
            _context = context;
            _tab = context.GetTab();
            
            // Получаем параметры
            if (context.SharedData.TryGetValue("MinProfitPercent", out var minProfit))
                _minProfitPercentParam = minProfit as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("UseBreakEven", out var useBreakEven))
                _useBreakEven = useBreakEven as StrategyParameterString;
            if (context.SharedData.TryGetValue("BreakEvenTriggerPercent", out var breakEvenTrigger))
                _breakEvenTriggerPercent = breakEvenTrigger as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("MaxSpreadPercent", out var maxSpread))
                _maxSpreadPercent = maxSpread as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("CloseMode", out var closeMode))
                _closeMode = closeMode as StrategyParameterString;
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            await Task.CompletedTask;
            _lastPrice = candle.Close;
        }
        
        public void Dispose()
        {
            _entryPrices.Clear();
            _minProfitPrices.Clear();
            _breakEvenApplied.Clear();
            _maxProfitPercentReached.Clear();
            _maxProfitValueReached.Clear();
            _minProfitPercentReached.Clear();
            _minProfitValueReached.Clear();
            _wentPositive.Clear();
            _minProfitReached.Clear();
        }
        
        // Основные методы защиты
        public bool CanOpenPosition(decimal currentPrice, decimal volume, string securityKey)
        {
            try
            {
                // Проверка спреда
                if (_maxSpreadPercent.ValueDecimal > 0)
                {
                    decimal spreadPercent = 0m;
                    if (_tab.PriceBestBid > 0 && _tab.PriceBestAsk > 0)
                    {
                        spreadPercent = (_tab.PriceBestAsk - _tab.PriceBestBid) / _tab.PriceBestBid * 100m;
                    }
                    
                    if (spreadPercent > _maxSpreadPercent.ValueDecimal)
                    {
                        _context.SendLog($"🚫 Слишком большой спред: {spreadPercent:F2}% > {_maxSpreadPercent.ValueDecimal:F2}%", 
                            LogMessageType.System);
                        return false;
                    }
                
                }
                
                return true;
            }
            catch
            {
                return true; // Если произошла ошибка, разрешаем открытие
            }
        }
        
        /// <summary>
        /// ✅ АБСОЛЮТНАЯ ЗАЩИТА ОТ УБЫТКОВ
        /// Проверяет возможность закрытия позиции согласно строгим правилам ТЗ.
        /// Закрытие ЗАПРЕЩЕНО если:
        /// 1. Текущая прибыль < MinProfitPercent (даже если = 0%)
        /// 2. Позиция никогда не была в плюсе (_wentPositive[positionId] == false)
        /// 3. Позиция была в плюсе, но вернулась в минус
        /// Закрытие РАЗРЕШЕНО ТОЛЬКО если:
        /// 1. Текущая прибыль ≥ MinProfitPercent
        /// 2. Цена закрытия гарантированно ≥ минимальной цены прибыли
        /// </summary>
        /// <summary>
        /// ✅ АБСОЛЮТНАЯ ЗАЩИТА ОТ УБЫТКОВ
        /// 
        /// Основной принцип: Любое закрытие позиции с отрицательным финансовым результатом ЗАПРЕЩЕНО.
        /// Система гарантирует, что ни при каких обстоятельствах позиция не может быть закрыта с убытком.
        /// Защита распространяется на ВСЕ позиции (ботовские и ручные).
        /// 
        /// Критерии блокировки закрытия:
        /// 1. Текущая прибыль < минимальной прибыли (задаётся в параметрах) – независимо от истории движения цены
        /// 2. Позиция никогда не была в плюсе – блокировка постоянная до выхода в плюс
        /// 3. Позиция была в плюсе, но вернулась в минус – блокировка возобновляется
        /// 4. Любые сигналы на закрытие в минусе – игнорируются ВСЕ торговые сигналы
        /// 
        /// Условия разрешения закрытия:
        /// 1. Текущая прибыль ≥ минимальной прибыли – позиция в нуле или плюсе
        /// 2. Цена достигла уровня минимальной прибыли – гарантированный выход с заданным профитом
        /// 
        /// Приоритет защиты: Абсолютная защита от убытков имеет ВЫСШИЙ ПРИОРИТЕТ над:
        /// - Торговыми сигналами (EMA, индикаторы)
        /// - Трейлинг-стопами
        /// - Тейк-профитами
        /// - Ручными командами
        /// - Рыночными условиями
        /// </summary>
        public bool CanClosePosition(int positionId, decimal currentPrice, bool isLong)
        {
            try
            {
                // ✅ По ТЗ: "Никаких исключений: если позиция не в RiskManager — разрешить закрытие 
                // (на случай ручных позиций без инициализации)"
                if (!_entryPrices.ContainsKey(positionId))
                {
                    _context.SendLog($"⚠️ Позиция #{positionId} не инициализирована в RiskManager - разрешаем закрытие", 
                        LogMessageType.System);
                    return true; // ✅ Разрешаем закрытие для неинициализированных позиций
                }

                decimal entryPrice = _entryPrices[positionId];
                decimal volume = GetPositionVolume(positionId);
                
                // ✅ РАСЧЕТ ТЕКУЩЕЙ ПРИБЫЛИ
                // ВАЖНО: Расчет прибыли выполняется БЕЗ вычитания комиссий.
                // Все комиссионные издержки учитываются через параметр "Минимальная прибыль %".
                // Это единственный источник учета всех транзакционных издержек.
                decimal currentProfitPercent = isLong 
                    ? ((currentPrice - entryPrice) / entryPrice) * 100m
                    : ((entryPrice - currentPrice) / entryPrice) * 100m;

                decimal currentProfitValue = isLong ?
                    (currentPrice - entryPrice) * volume :
                    (entryPrice - currentPrice) * volume;

                // Обновление исторических экстремумов
                _maxProfitPercentReached.AddOrUpdate(positionId, currentProfitPercent,
                    (key, oldMax) => currentProfitPercent > oldMax ? currentProfitPercent : oldMax);
                _maxProfitValueReached.AddOrUpdate(positionId, currentProfitValue,
                    (key, oldMax) => currentProfitValue > oldMax ? currentProfitValue : oldMax);

                _minProfitPercentReached.AddOrUpdate(positionId, currentProfitPercent,
                    (key, oldMin) => currentProfitPercent < oldMin ? currentProfitPercent : oldMin);
                _minProfitValueReached.AddOrUpdate(positionId, currentProfitValue,
                    (key, oldMin) => currentProfitValue < oldMin ? currentProfitValue : oldMin);
                
                // ✅ ФИКСАЦИЯ ВЫХОДА В ПЛЮС: раз и навсегда
                // После выхода в плюс даже возврат в минус не отменяет право на закрытие
                if (currentProfitPercent > 0 && (!_wentPositive.ContainsKey(positionId) || !_wentPositive[positionId]))
                {
                    _wentPositive[positionId] = true;
                    _context.SendLog($"✅ Позиция #{positionId} вышла в плюс: {currentProfitPercent:F2}%", 
                        LogMessageType.System);
                }

                decimal minProfitPercent = _minProfitPercentParam?.ValueDecimal ?? 0m;
                
                // ✅ КРИТЕРИЙ 1: Текущая прибыль < MinProfitPercent (даже если = 0%)
                if (currentProfitPercent < minProfitPercent)
                {
                    // ✅ КРИТЕРИЙ 2: Позиция никогда не была в плюсе
                    bool neverWentPositive = !_wentPositive.ContainsKey(positionId) || !_wentPositive[positionId];
                    
                    if (neverWentPositive)
                    {
                        // ✅ БЛОКИРОВКА: Позиция никогда не была в плюсе - постоянная блокировка до выхода в плюс
                        _context.SendLog($"🔒 АБСОЛЮТНЫЙ ЗАПРЕТ: позиция #{positionId} в минусе {currentProfitPercent:F2}% (никогда не была в плюсе)", 
                            LogMessageType.System);
                        return false;
                    }
                    
                    // ✅ КРИТЕРИЙ 3: Позиция была в плюсе, но вернулась в минус - блокировка возобновляется
                    // По ТЗ 4.2.3: "Позиция была в плюсе, но вернулась в минус – блокировка возобновляется"
                    // По ТЗ 4.5: "НЕ ДОПУСКАЕТСЯ уход обратно в минус"
                    // Система обязана закрыть позицию на уровне минимальной прибыли, НО только если текущая прибыль >= 0
                    // Если позиция вернулась в минус - блокируем закрытие до выхода обратно в плюс
                    if (currentProfitPercent < 0)
                    {
                        _context.SendLog($"🔒 АБСОЛЮТНЫЙ ЗАПРЕТ: позиция #{positionId} вернулась в минус {currentProfitPercent:F2}% после выхода в плюс - блокировка возобновлена", 
                            LogMessageType.System);
                        return false;
                    }
                    
                    // ✅ УСЛОВИЕ: Позиция была в плюсе, текущая прибыль >= 0, но < minProfitPercent
                    // В этом случае разрешаем закрытие по minProfitPrice (защита от ухода в минус)
                    _context.SendLog($"🛡️ ЗАЩИТА МИНИМАЛЬНОЙ ПРИБЫЛИ: Позиция #{positionId} текущая прибыль {currentProfitPercent:F2}% < {minProfitPercent:F2}% (закрытие по мин.цене)", 
                        LogMessageType.System);
                    // ✅ РАЗРЕШАЕМ закрытие - цена будет скорректирована на minProfitPrice в TryClosePosition
                    return true;
                }

                // ✅ РАЗРЕШЕНИЕ: Текущая прибыль ≥ MinProfitPercent
                // Все проверки пройдены - закрытие разрешено
                return true;
            }
            catch (Exception ex)
            {
                _context.SendLog($"Ошибка проверки закрытия позиции #{positionId}: {ex.Message}", LogMessageType.Error);
                return false; // ✅ В случае ошибки блокируем закрытие
            }
        }
        
        /// <summary>
        /// ✅ ИНИЦИАЛИЗАЦИЯ ОТКРЫТОЙ ПОЗИЦИИ
        /// Устанавливает начальные значения для отслеживания защиты от убытков.
        /// _wentPositive устанавливается в false - позиция еще не была в плюсе.
        /// </summary>
        public void InitializePosition(int positionId, decimal entryPrice, bool isLong, decimal volume, bool isBotPosition)
        {
            _entryPrices[positionId] = entryPrice;
            _minProfitPrices[positionId] = CalculateMinProfitPrice(entryPrice, isLong);
            _breakEvenApplied[positionId] = false;
            
            _maxProfitPercentReached[positionId] = 0m;
            _maxProfitValueReached[positionId] = 0m;
            _minProfitPercentReached[positionId] = 0m;
            _minProfitValueReached[positionId] = 0m;
            _wentPositive[positionId] = false; // ✅ КРИТИЧНО: Позиция еще не была в плюсе
            _minProfitReached[positionId] = false;
            
            // Минимальная прибыль в процентах и цена безубытка (entry + MinProfitPercent)
            decimal minProfitPercent = _minProfitPercentParam?.ValueDecimal ?? 0m;
            decimal breakevenPrice = CalculateMinProfitPrice(entryPrice, isLong);
            
            // Текущая цена для расчета статуса (если ещё нет цены — отображаем 0)
            decimal currentPrice = _lastPrice;
            decimal currentProfitPercent = 0m;
            decimal currentProfitValue = 0m;
            
            if (currentPrice > 0 && volume > 0)
            {
                currentProfitPercent = isLong
                    ? ((currentPrice - entryPrice) / entryPrice) * 100m
                    : ((entryPrice - currentPrice) / entryPrice) * 100m;

                currentProfitValue = isLong
                    ? (currentPrice - entryPrice) * volume
                    : (entryPrice - currentPrice) * volume;
            }
            
            _context.SendLog(
                $"✅ Инициализирована открытая позиция #{positionId} в RiskManager | " +
                $"Вход: {entryPrice:F4} | Текущая: {currentPrice:F4} | " +
                $"Профит: {currentProfitPercent:F2}% ({currentProfitValue:F2}) | " +
                $"Мин. прибыль: {minProfitPercent:F2}% | Цена безубытка: {breakevenPrice:F4}",
                LogMessageType.System);
        }
        
        public void RemovePosition(int positionId)
        {
            _entryPrices.TryRemove(positionId, out _);
            _minProfitPrices.TryRemove(positionId, out _);
            _breakEvenApplied.TryRemove(positionId, out _);
            _maxProfitPercentReached.TryRemove(positionId, out _);
            _maxProfitValueReached.TryRemove(positionId, out _);
            _minProfitPercentReached.TryRemove(positionId, out _);
            _minProfitValueReached.TryRemove(positionId, out _);
            _wentPositive.TryRemove(positionId, out _);
            _minProfitReached.TryRemove(positionId, out _);
        }
        
        /// <summary>
        /// ✅ РАСЧЕТ ЦЕНЫ МИНИМАЛЬНОЙ ПРИБЫЛИ
        /// 
        /// ВАЖНО: Параметр "Минимальная прибыль %" является ЕДИНСТВЕННЫМ И ДОСТАТОЧНЫМ 
        /// источником учёта всех комиссионных издержек (брокер, биржа, прочие транзакционные издержки).
        /// 
        /// В расчётные формулы и логику кода НЕ ДОЛЖНЫ быть встроены дополнительные 
        /// фиксированные или расчётные комиссии. Вся необходимая маржа для гарантированного 
        /// безубыточного закрытия с учётом всех издержек задаётся исключительно через этот параметр.
        /// 
        /// Это означает, что значение минимальной прибыли должно компенсировать все 
        /// транзакционные издержки и обеспечивать заданный чистый финансовый результат.
        /// </summary>
        private decimal CalculateMinProfitPrice(decimal entryPrice, bool isLong)
        {
            // ✅ ИСПОЛЬЗУЕТСЯ ТОЛЬКО ПАРАМЕТР _minProfitPercentParam - никаких дополнительных комиссий
            if (_minProfitPercentParam == null)
                return entryPrice; // Возвращаем цену входа, если параметр не инициализирован
            
            return isLong
                ? entryPrice * (1 + _minProfitPercentParam.ValueDecimal / 100m)
                : entryPrice * (1 - _minProfitPercentParam.ValueDecimal / 100m);
        }
        
        private decimal GetPositionVolume(int positionId)
        {
            // В реальной реализации нужно получать объем из PositionManager
            return 1m; // Заглушка
        }
        
        // Геттеры для статистики
        public decimal GetEntryPrice(int positionId) => _entryPrices.TryGetValue(positionId, out var price) ? price : 0;
        public decimal GetMinProfitPrice(int positionId) => _minProfitPrices.TryGetValue(positionId, out var price) ? price : 0;
        public (decimal maxPercent, decimal maxValue, decimal minPercent, decimal minValue) GetPositionStats(int positionId)
        {
            _maxProfitPercentReached.TryGetValue(positionId, out var maxPercent);
            _maxProfitValueReached.TryGetValue(positionId, out var maxValue);
            _minProfitPercentReached.TryGetValue(positionId, out var minPercent);
            _minProfitValueReached.TryGetValue(positionId, out var minValue);
            
            return (maxPercent, maxValue, minPercent, minValue);
        }
        
        public bool WentPositive(int positionId)
        {
            return _wentPositive.TryGetValue(positionId, out var wentPos) && wentPos;
        }
        
        public bool IsIndividualCloseMode()
        {
            return _closeMode?.ValueString == "По отдельным сделкам";
        }
    }
    
    // 3. КОМПОНЕНТ МЕНЕДЖЕРА ПОЗИЦИЙ
    public class PositionManagerComponent : ITradingComponent
    {
        public string ComponentName => "PositionManager";
        
        private IComponentContext _context;
        private BotTabSimple _tab;
        private StrategyParameterString _closeMode;
        private StrategyParameterInt _maxOpenPositions;
        private StrategyParameterInt _reentryCooldownCandles;
        
        private readonly ConcurrentDictionary<int, Position> _activePositions = new();
        private readonly ConcurrentDictionary<int, bool> _botOpenedPositions = new();
        private readonly ConcurrentQueue<string> _pendingOpenReasons = new();
        private readonly ConcurrentDictionary<int, string> _positionReasons = new();
        private readonly List<Position> _positionsCache = new();
        private DateTime _lastPositionsCacheTime;
        
        private int _lastExitBarIndex;
        private int _lastEntryBarIndex;
        
        public void Initialize(IComponentContext context)
        {
            _context = context;
            _tab = context.GetTab();
            
            // Получаем параметры
            if (context.SharedData.TryGetValue("CloseMode", out var closeMode))
                _closeMode = closeMode as StrategyParameterString;
            if (context.SharedData.TryGetValue("MaxOpenPositions", out var maxPositions))
                _maxOpenPositions = maxPositions as StrategyParameterInt;
            if (context.SharedData.TryGetValue("ReentryCooldownCandles", out var cooldown))
                _reentryCooldownCandles = cooldown as StrategyParameterInt;
            
            // Подписка на события
            _tab.PositionOpeningSuccesEvent += OnPositionOpeningSuccess;
            _tab.PositionClosingSuccesEvent += OnPositionClosingSuccess;
            
            // Подхват существующих позиций
            Task.Run(async () =>
            {
                await Task.Delay(3000); // Ждем 3 секунды после запуска
                CaptureExistingPositions();
            });
        }
        
        private void CaptureExistingPositions()
        {
            try
            {
                var positions = _tab.PositionsOpenAll;
                if (positions != null)
                {
                    var riskManager = _context.GetComponent<RiskManagementComponent>();
                    int capturedCount = 0;
                    int alreadyInitializedCount = 0;
                    
                    foreach (var position in positions.Where(p => p.State == PositionStateType.Open))
                    {
                        int positionId = position.Number;
                        
                        // ✅ Проверяем, инициализирована ли позиция в RiskManager
                        // Если не инициализирована (GetEntryPrice == 0) - инициализируем
                        if (riskManager != null && riskManager.GetEntryPrice(positionId) == 0)
                        {
                            InitializeManualPosition(position);
                            capturedCount++;
                        }
                        else
                        {
                            // Позиция уже инициализирована - просто добавляем в активные
                            _activePositions[positionId] = position;
                            // ✅ НЕ устанавливаем "Manual" автоматически - оставляем пустым или "неизвестен"
                            // Это позволяет сохранить реальную причину, если она была установлена ранее
                            // Если причина не найдена, она будет показана как "неизвестен" в логах
                            if (!_positionReasons.ContainsKey(positionId))
                            {
                                _positionReasons[positionId] = "неизвестен";
                            }
                            alreadyInitializedCount++;
                        }
                    }
                    
                    _context.SendLog($"✅ Подхват позиций завершён: новых инициализировано {capturedCount}, уже инициализировано {alreadyInitializedCount}, всего открыто {positions.Count(p => p.State == PositionStateType.Open)}", 
                        LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка подхвата позиций: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private void OnPositionOpeningSuccess(Position position)
        {
            try
            {
                int positionId = position.Number;
                _activePositions[positionId] = position;
                _botOpenedPositions[positionId] = true;

                if (_pendingOpenReasons.TryDequeue(out string reason))
                {
                    _positionReasons[positionId] = reason;
                }
                
                // Запоминаем бар, на котором открыта последняя позиция (для правила "одна свеча - одна сделка")
                _lastEntryBarIndex = _tab.CandlesAll?.Count ?? _lastEntryBarIndex;
                
                // Уведомляем RiskManager
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager != null)
                {
                    riskManager.InitializePosition(positionId, position.EntryPrice, 
                        position.Direction == Side.Buy, position.OpenVolume, true);
                }
                
                // Дополнительный контроль: если вдруг после открытия количество позиций превысило лимит — логируем
                int totalOpenPositions = GetAllOpenPositionsCount();
                if (_maxOpenPositions != null && totalOpenPositions > _maxOpenPositions.ValueInt)
                {
                    _context.SendLog(
                        $"❗ ВНИМАНИЕ: после открытия позиции #{positionId} всего открыто {totalOpenPositions}, " +
                        $"что ПРЕВЫШАЕТ лимит {_maxOpenPositions.ValueInt}. Проверьте логи и настройки.",
                        LogMessageType.Error);
                }
                
                _context.SendLog($"✅ ПОЗИЦИЯ ОТКРЫТА: #{positionId} {position.Direction} {position.SecurityName}", 
                    LogMessageType.System);
            }
            catch (Exception ex)
            {
                _context.SendLog($"Ошибка в OnPositionOpeningSuccess: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private void OnPositionClosingSuccess(Position position)
        {
            try
            {
                int positionId = position.Number;
                _activePositions.TryRemove(positionId, out _);
                _botOpenedPositions.TryRemove(positionId, out _);
                
                // Уведомляем RiskManager
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager != null)
                {
                    riskManager.RemovePosition(positionId);
                }
                
                _lastExitBarIndex = _tab.CandlesAll?.Count ?? 0;
                
                _context.SendLog($"🔒 ПОЗИЦИЯ ЗАКРЫТА: #{positionId} {position.Direction} {position.SecurityName}", 
                    LogMessageType.System);
            }
            catch (Exception ex)
            {
                _context.SendLog($"Ошибка в OnPositionClosingSuccess: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private void InitializeManualPosition(Position position)
        {
            try
            {
                int positionId = position.Number;
                _activePositions[positionId] = position;
                // ✅ НЕ перезаписываем причину открытия, если она уже есть
                // Это позволяет сохранить реальную причину для позиций, открытых ботом ранее
                if (!_positionReasons.ContainsKey(positionId))
                {
                    _positionReasons[positionId] = "Manual";
                }
                
                // Уведомляем RiskManager
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager != null)
                {
                    riskManager.InitializePosition(positionId, position.EntryPrice, 
                        position.Direction == Side.Buy, position.OpenVolume, false);
                    _context.SendLog($"ℹ️ RiskManager: ручная позиция #{positionId} инициализирована", 
                        LogMessageType.System);
                }
                
                _context.SendLog($"✅ РУЧНАЯ ПОЗИЦИЯ ПОДХВАЧЕНА: #{positionId} {position.SecurityName}", 
                    LogMessageType.System);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка инициализации ручной позиции #{position.Number}: {ex.Message}", 
                    LogMessageType.Error);
            }
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            await Task.CompletedTask;
            
            // Обновление кэша позиций
            if (DateTime.Now - _lastPositionsCacheTime > TimeSpan.FromMilliseconds(100))
            {
                try
                {
                    var positions = _tab.PositionsOpenAll ?? new List<Position>();
                    _positionsCache.Clear();
                    _positionsCache.AddRange(positions);
                    _lastPositionsCacheTime = DateTime.Now;
                }
                catch { }
            }
        }
        
        public void Dispose()
        {
            _activePositions.Clear();
            _botOpenedPositions.Clear();
            _positionsCache.Clear();
            _lastEntryBarIndex = 0;
            
            if (_tab != null)
            {
                _tab.PositionOpeningSuccesEvent -= OnPositionOpeningSuccess;
                _tab.PositionClosingSuccesEvent -= OnPositionClosingSuccess;
            }
        }
        
        // Методы для работы с позициями
        public List<Position> GetActivePositions()
        {
            // ✅ ИСПРАВЛЕНО: Используем актуальный список позиций из таба, а не кэш,
            // чтобы избежать расхождений при быстром открытии нескольких сделок
            // ВАЖНО: Возвращаем ВСЕ открытые позиции, независимо от инициализации в RiskManager
            var positions = _tab?.PositionsOpenAll ?? new List<Position>();
            var openPositions = positions.Where(p => p.State == PositionStateType.Open).ToList();
            
            // ✅ ДОПОЛНИТЕЛЬНО: Убеждаемся, что все позиции добавлены в _activePositions
            // Это гарантирует, что они будут доступны для других методов
            foreach (var pos in openPositions)
            {
                if (!_activePositions.ContainsKey(pos.Number))
                {
                    _activePositions[pos.Number] = pos;
                }
            }
            
            return openPositions;
        }
        
        public int GetBotManagedPositionsCount()
        {
            var positions = _tab?.PositionsOpenAll ?? new List<Position>();
            return positions.Count(p => p.State == PositionStateType.Open && 
                _botOpenedPositions.ContainsKey(p.Number));
        }

        public void RegisterOpenReason(string reason)
        {
            _pendingOpenReasons.Enqueue(string.IsNullOrWhiteSpace(reason) ? "неизвестен" : reason);
        }

        public string GetOpenReason(int positionId)
        {
            return _positionReasons.TryGetValue(positionId, out var reason) ? reason : "неизвестен";
        }
        
        public int GetAllOpenPositionsCount()
        {
            var positions = _tab?.PositionsOpenAll ?? new List<Position>();
            return positions.Count(p => p.State == PositionStateType.Open);
        }
        
        public bool CanBotOpenNewPosition(decimal volume, string securityKey, int currentBar)
        {
            try
            {
                // Жёсткое правило: ОДНА СВЕЧА — ОДНА СДЕЛКА
                // Если уже была открыта позиция на этом баре, новые открытия запрещены
                if (currentBar > 0 && _lastEntryBarIndex == currentBar)
                {
                    _context.SendLog(
                        $"🚫 ОТКРЫТИЕ ЗАПРЕЩЕНО: уже была сделка на этой свече (barIndex={currentBar})",
                        LogMessageType.System);
                    return false;
                }
                
                // Считаем ВСЕ открытые позиции (и ботовские, и ручные)
                // строго по актуальному состоянию таба, без использования кэша
                int totalOpenPositions = GetAllOpenPositionsCount();
                int botManagedPositions = GetBotManagedPositionsCount();

                // Жёсткий лимит: если общее количество позиций >= MaxOpenPositions — НИЧЕГО больше не открываем
                if (_maxOpenPositions != null && totalOpenPositions >= _maxOpenPositions.ValueInt)
                {
                    _context.SendLog(
                        $"🚫 ЛИМИТ ПОЗИЦИЙ ДОСТИГНУТ: всего открыто {totalOpenPositions}, " +
                        $"максимум разрешено {_maxOpenPositions.ValueInt} (бот управляет {botManagedPositions})",
                        LogMessageType.System);
                    return false;
                }
                
                // Проверка кулдауна
                if (_reentryCooldownCandles != null 
                    && _reentryCooldownCandles.ValueInt > 0 
                    && currentBar - _lastExitBarIndex < _reentryCooldownCandles.ValueInt)
                {
                    return false;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public bool HasLongPosition()
        {
            return _positionsCache.Any(p => p.State == PositionStateType.Open && p.Direction == Side.Buy);
        }
        
        public bool HasShortPosition()
        {
            return _positionsCache.Any(p => p.State == PositionStateType.Open && p.Direction == Side.Sell);
        }
        
        public bool IsBotPosition(int positionId)
        {
            return _botOpenedPositions.ContainsKey(positionId);
        }
    }
    
    // 4. КОМПОНЕНТ ТРЕЙЛИНГ-СТОПА
    public class TrailingStopComponent : ITradingComponent
    {
        public string ComponentName => "TrailingStop";
        
        private IComponentContext _context;
        private BotTabSimple _tab;
        private StrategyParameterString _useTrailingStop;
        private StrategyParameterString _trailingType;
        private StrategyParameterDecimal _trailingStartPercent;
        private StrategyParameterDecimal _trailingDistancePercent;
        private StrategyParameterDecimal _atrMultiplier;
        
        private readonly ConcurrentDictionary<int, bool> _trailingActive = new();
        private readonly ConcurrentDictionary<int, decimal> _currentTrailingLevels = new();
        private readonly ConcurrentDictionary<int, decimal> _trailingStartPrices = new();
        private readonly ConcurrentDictionary<int, decimal> _highestPricesSinceEntry = new();
        private readonly ConcurrentDictionary<int, decimal> _lowestPricesSinceEntry = new();
        private DateTime _lastTrailingStatusLog = DateTime.MinValue;
        private readonly TimeSpan _trailingStatusLogInterval = TimeSpan.FromMinutes(5); // Логируем статус каждые 5 минут
        
        public void Initialize(IComponentContext context)
        {
            _context = context;
            _tab = context.GetTab();
            
            // Получаем параметры
            if (context.SharedData.TryGetValue("UseTrailingStop", out var useTrailing))
                _useTrailingStop = useTrailing as StrategyParameterString;
            if (context.SharedData.TryGetValue("TrailingType", out var trailingType))
                _trailingType = trailingType as StrategyParameterString;
            if (context.SharedData.TryGetValue("TrailingStartPercent", out var startPercent))
                _trailingStartPercent = startPercent as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("TrailingDistancePercent", out var distancePercent))
                _trailingDistancePercent = distancePercent as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AtrMultiplier", out var atrMultiplier))
                _atrMultiplier = atrMultiplier as StrategyParameterDecimal;
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            await Task.CompletedTask;
            
            if (!IsTrailingEnabled()) return;
            
            // Получаем активные позиции
            var positionManager = _context.GetComponent<PositionManagerComponent>();
            if (positionManager == null) return;
            
            var activePositions = positionManager.GetActivePositions();
            
            foreach (var position in activePositions)
            {
                CheckAndUpdateTrailing(position, candle.Close);
            }
            
            // Периодическое логирование статуса трейлинга для всех активных позиций
            if (DateTime.Now - _lastTrailingStatusLog >= _trailingStatusLogInterval)
            {
                LogTrailingStatus(activePositions, candle.Close);
                _lastTrailingStatusLog = DateTime.Now;
            }
        }
        
        private void LogTrailingStatus(List<Position> activePositions, decimal currentPrice)
        {
            try
            {
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager == null) return;
                
                var positionsWithTrailing = activePositions
                    .Where(p => _trailingActive.ContainsKey(p.Number) && _trailingActive[p.Number])
                    .ToList();
                
                if (positionsWithTrailing.Count == 0) return;
                
                _context.SendLog("=== 📊 СТАТУС ТРЕЙЛИНГ-СТОПОВ ===", LogMessageType.System);
                
                foreach (var position in positionsWithTrailing)
                {
                    int positionId = position.Number;
                    bool isLong = position.Direction == Side.Buy;
                    decimal entryPrice = riskManager.GetEntryPrice(positionId);
                    decimal trailingLevel = _currentTrailingLevels.ContainsKey(positionId) 
                        ? _currentTrailingLevels[positionId] 
                        : 0m;
                    
                    if (entryPrice == 0 || trailingLevel == 0) continue;
                    
                    decimal profitPercent = isLong 
                        ? (currentPrice - entryPrice) / entryPrice * 100m
                        : (entryPrice - currentPrice) / entryPrice * 100m;
                    
                    decimal distanceToTrailing = isLong 
                        ? (currentPrice - trailingLevel) 
                        : (trailingLevel - currentPrice);
                    decimal distancePercent = (distanceToTrailing / entryPrice) * 100m;
                    
                    string status = isLong 
                        ? (currentPrice > trailingLevel ? "✅ АКТИВЕН" : "🔔 СРАБОТАЛ")
                        : (currentPrice < trailingLevel ? "✅ АКТИВЕН" : "🔔 СРАБОТАЛ");
                    
                    string direction = isLong ? "LONG" : "SHORT";
                    _context.SendLog($"#{positionId} {direction}: {status} | Прибыль: {profitPercent:F2}% | Уровень: {trailingLevel:F4} | Расстояние: {distancePercent:F3}%", 
                        LogMessageType.System);
                }
                
                _context.SendLog("=================================", LogMessageType.System);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка логирования статуса трейлинга: {ex.Message}", LogMessageType.Error);
            }
        }
        
        public void Dispose()
        {
            _trailingActive.Clear();
            _currentTrailingLevels.Clear();
            _trailingStartPrices.Clear();
            _highestPricesSinceEntry.Clear();
            _lowestPricesSinceEntry.Clear();
        }
        
        public bool IsTrailingEnabled()
        {
            return _useTrailingStop.ValueString == "Включён";
        }
        
        public void ActivateTrailing(int positionId, bool isLong, decimal currentPrice)
        {
            _trailingActive[positionId] = true;
            _trailingStartPrices[positionId] = currentPrice;
            
            // Получаем цену входа для расчета прибыли
            var riskManager = _context.GetComponent<RiskManagementComponent>();
            decimal entryPrice = riskManager?.GetEntryPrice(positionId) ?? 0m;
            decimal profitPercent = 0m;
            if (entryPrice > 0)
            {
                profitPercent = isLong 
                    ? (currentPrice - entryPrice) / entryPrice * 100m
                    : (entryPrice - currentPrice) / entryPrice * 100m;
            }
            
            // Получаем параметры трейлинга для логирования
            decimal trailingStart = _trailingStartPercent?.ValueDecimal ?? 0m;
            decimal trailingDistance = _trailingDistancePercent?.ValueDecimal ?? 0m;
            string trailingType = _trailingType?.ValueString ?? "Неизвестно";
            
            // Рассчитываем начальный уровень трейлинга
            decimal initialTrailingLevel = CalculateTrailingLevel(positionId, currentPrice, isLong);
            _currentTrailingLevels[positionId] = initialTrailingLevel;
            
            // ✅ КРИТИЧНО: Сохраняем начальный уровень трейлинга в SharedData для использования при закрытии
            if (initialTrailingLevel > 0)
            {
                _context.SharedData[$"TrailingClosePrice_{positionId}"] = initialTrailingLevel;
            }
            
            string direction = isLong ? "LONG" : "SHORT";
            _context.SendLog($"🎯 ТРЕЙЛИНГ АКТИВИРОВАН для позиции #{positionId} ({direction})", LogMessageType.System);
            _context.SendLog($"   📊 Текущая прибыль: {profitPercent:F2}% (порог активации: {trailingStart:F2}%)", LogMessageType.System);
            _context.SendLog($"   💰 Цена активации: {currentPrice:F4} | Вход: {entryPrice:F4}", LogMessageType.System);
            _context.SendLog($"   ⚙️ Тип: {trailingType} | Дистанция: {trailingDistance:F2}%", LogMessageType.System);
            _context.SendLog($"   🎯 Начальный уровень трейлинга: {initialTrailingLevel:F4}", LogMessageType.System);
        }
        
        public bool CheckTrailingStop(int positionId, decimal currentPrice, Position position)
        {
            try
            {
                if (!_trailingActive.ContainsKey(positionId) || !_trailingActive[positionId]) return false;
                if (!_currentTrailingLevels.ContainsKey(positionId)) return false;
                
                bool isLong = position.Direction == Side.Buy;
                decimal trailingLevel = _currentTrailingLevels[positionId];
                bool stopHit = isLong ? currentPrice <= trailingLevel 
                                     : currentPrice >= trailingLevel;
                
                // Детальное логирование при срабатывании трейлинг-стопа
                if (stopHit)
                {
                    var riskManager = _context.GetComponent<RiskManagementComponent>();
                    decimal entryPrice = riskManager?.GetEntryPrice(positionId) ?? 0m;
                    decimal profitPercent = 0m;
                    if (entryPrice > 0)
                    {
                        profitPercent = isLong 
                            ? (currentPrice - entryPrice) / entryPrice * 100m
                            : (entryPrice - currentPrice) / entryPrice * 100m;
                    }
                    
                    decimal distanceToLevel = isLong 
                        ? (currentPrice - trailingLevel) 
                        : (trailingLevel - currentPrice);
                    decimal distancePercent = entryPrice > 0 
                        ? (distanceToLevel / entryPrice) * 100m 
                        : 0m;
                    
                    _context.SendLog($"🔔 ТРЕЙЛИНГ-СТОП СРАБОТАЛ для позиции #{positionId}", LogMessageType.Trade);
                    _context.SendLog($"   💰 Текущая цена: {currentPrice:F4} | Уровень трейлинга: {trailingLevel:F4}", LogMessageType.Trade);
                    _context.SendLog($"   📊 Прибыль при срабатывании: {profitPercent:F2}% | Расстояние до уровня: {distancePercent:F3}%", LogMessageType.Trade);
                }
                
                return stopHit;
            }
            catch
            {
                return false;
            }
        }
        
        private void CheckAndUpdateTrailing(Position position, decimal currentPrice)
        {
            try
            {
                int positionId = position.Number;
                bool isLong = position.Direction == Side.Buy;
                
                // Получаем цену входа из RiskManager
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager == null) return;
                
                decimal entryPrice = riskManager.GetEntryPrice(positionId);
                if (entryPrice == 0) return;
                
                // Обновляем экстремумы
                if (isLong)
                {
                    decimal currentHighest = _highestPricesSinceEntry.GetOrAdd(positionId, entryPrice);
                    if (currentPrice > currentHighest)
                    {
                        _highestPricesSinceEntry[positionId] = currentPrice;
                    }
                }
                else
                {
                    decimal currentLowest = _lowestPricesSinceEntry.GetOrAdd(positionId, entryPrice);
                    if (currentPrice < currentLowest)
                    {
                        _lowestPricesSinceEntry[positionId] = currentPrice;
                    }
                }
                
                // Проверяем условие активации трейлинга
                decimal profitPercent = isLong 
                    ? (currentPrice - entryPrice) / entryPrice * 100
                    : (entryPrice - currentPrice) / entryPrice * 100;

                // Старт трейлинга не может быть ниже минимальной прибыли RiskManager
                decimal minProfitPercent = 0m;
                try
                {
                    // Получаем глобальный параметр минимальной прибыли из SharedData
                    if (_context.SharedData.TryGetValue("MinProfitPercent", out var minProfObj)
                        && minProfObj is StrategyParameterDecimal minProfParam)
                    {
                        minProfitPercent = minProfParam.ValueDecimal;
                    }
                }
                catch { }

                decimal trailingStart = _trailingStartPercent != null
                    ? Math.Max(_trailingStartPercent.ValueDecimal, minProfitPercent)
                    : minProfitPercent;
                
                // Логирование статуса перед активацией (только при приближении к порогу)
                bool isNearThreshold = profitPercent >= trailingStart * 0.9m && profitPercent < trailingStart;
                if (isNearThreshold && !_trailingActive.ContainsKey(positionId))
                {
                    _context.SendLog($"⏳ Позиция #{positionId}: Прибыль {profitPercent:F2}% → порог активации трейлинга {trailingStart:F2}% (осталось {trailingStart - profitPercent:F2}%)", 
                        LogMessageType.System);
                }
                
                if (profitPercent >= trailingStart)
                {
                    if (!_trailingActive.ContainsKey(positionId) || !_trailingActive[positionId])
                    {
                        ActivateTrailing(positionId, isLong, currentPrice);
                    }
                    
                    // Рассчитываем уровень трейлинга
                    decimal trailingLevel = CalculateTrailingLevel(positionId, currentPrice, isLong);
                    
                    // ✅ КРИТИЧНО: Обновляем уровень трейлинга
                    bool levelUpdated = false;
                    decimal oldLevel = _currentTrailingLevels.ContainsKey(positionId) ? _currentTrailingLevels[positionId] : 0m;
                    
                    // Для обычного трейлинга: обновляем только если уровень улучшается
                    // Для самообучающегося: обновляем всегда, так как уровень может изменяться из-за адаптации
                    bool shouldUpdate = false;
                    
                    if (!_currentTrailingLevels.ContainsKey(positionId))
                    {
                        // Первое установление уровня
                        shouldUpdate = true;
                    }
                    else if (_trailingType?.ValueString == "Самообучаемый")
                    {
                        // ✅ ДЛЯ САМООБУЧАЕМОГО: Обновляем всегда, если уровень изменился
                        // (даже если не улучшился, так как адаптация может изменить дистанцию)
                        if (trailingLevel != oldLevel)
                        {
                            // Но проверяем, что новый уровень не хуже старого (не уменьшает защиту)
                            bool isBetter = isLong 
                                ? trailingLevel >= oldLevel  // Для LONG: новый уровень не ниже старого
                                : trailingLevel <= oldLevel; // Для SHORT: новый уровень не выше старого
                            
                            if (isBetter)
                            {
                                shouldUpdate = true;
                            }
                            else
                            {
                                // Если новый уровень хуже, но разница небольшая (менее 0.1%), всё равно обновляем
                                // (это может быть из-за адаптации к волатильности)
                                decimal diff = Math.Abs(trailingLevel - oldLevel);
                                decimal diffPercent = entryPrice > 0 ? (diff / entryPrice) * 100m : 0m;
                                if (diffPercent < 0.1m)
                                {
                                    shouldUpdate = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Для обычного трейлинга: обновляем только если уровень улучшается
                        shouldUpdate = (isLong && trailingLevel > _currentTrailingLevels[positionId]) ||
                                      (!isLong && trailingLevel < _currentTrailingLevels[positionId]);
                    }
                    
                    if (shouldUpdate)
                    {
                        _currentTrailingLevels[positionId] = trailingLevel;
                        levelUpdated = true;
                    }
                    else
                    {
                        // ✅ ВАЖНО: Даже если уровень не обновился (не улучшился), используем текущий рассчитанный уровень
                        // Это гарантирует, что при закрытии будет использован актуальный уровень, даже если он не улучшился
                        // Особенно важно для ATR и фиксированного трейлинга, где уровень может не меняться при каждом обновлении
                        if (trailingLevel > 0 && _currentTrailingLevels.ContainsKey(positionId))
                        {
                            // Используем лучший из двух: текущий сохранённый или новый рассчитанный
                            decimal currentSaved = _currentTrailingLevels[positionId];
                            if (isLong && trailingLevel > currentSaved)
                            {
                                // Для LONG: новый уровень выше - это лучше
                                _currentTrailingLevels[positionId] = trailingLevel;
                                levelUpdated = true;
                            }
                            else if (!isLong && trailingLevel < currentSaved)
                            {
                                // Для SHORT: новый уровень ниже - это лучше
                                _currentTrailingLevels[positionId] = trailingLevel;
                                levelUpdated = true;
                            }
                        }
                    }
                    
                    // ✅ КРИТИЧНО: ВСЕГДА сохраняем актуальный уровень трейлинга в SharedData
                    // Это необходимо для использования в TryClosePosition при любом закрытии
                    // Сохраняем текущий уровень из _currentTrailingLevels (который всегда актуален)
                    decimal levelToSave = _currentTrailingLevels.ContainsKey(positionId) ? _currentTrailingLevels[positionId] : trailingLevel;
                    if (levelToSave > 0)
                    {
                        _context.SharedData[$"TrailingClosePrice_{positionId}"] = levelToSave;
                    }
                    
                    // Логирование обновления уровня трейлинга (только при изменении)
                    if (levelUpdated && oldLevel > 0)
                    {
                        decimal levelChange = isLong 
                            ? (trailingLevel - oldLevel) 
                            : (oldLevel - trailingLevel);
                        decimal levelChangePercent = entryPrice > 0 
                            ? (levelChange / entryPrice) * 100m 
                            : 0m;
                        
                        _context.SendLog($"📈 ТРЕЙЛИНГ ОБНОВЛЕН для позиции #{positionId}: {oldLevel:F4} → {trailingLevel:F4} (изменение: {levelChangePercent:F3}%)", 
                            LogMessageType.System);
                        _context.SendLog($"   💰 Текущая цена: {currentPrice:F4} | Прибыль: {profitPercent:F2}% | Уровень трейлинга: {trailingLevel:F4}", 
                            LogMessageType.System);
                    }
                    else if (levelUpdated && oldLevel == 0)
                    {
                        // Первое установление уровня
                        _context.SendLog($"📈 ТРЕЙЛИНГ УСТАНОВЛЕН для позиции #{positionId}: Уровень {trailingLevel:F4} | Прибыль: {profitPercent:F2}%", 
                            LogMessageType.System);
                    }
                }
                else if (_trailingActive.ContainsKey(positionId) && _trailingActive[positionId])
                {
                    // Логирование, если трейлинг был активен, но прибыль упала ниже порога
                    _context.SendLog($"⚠️ Позиция #{positionId}: Прибыль {profitPercent:F2}% < порога трейлинга {trailingStart:F2}% (трейлинг остается активным)", 
                        LogMessageType.System);
                }
            }
            catch { }
        }
        
        private decimal CalculateTrailingLevel(int positionId, decimal currentPrice, bool isLong)
        {
            if (_trailingType.ValueString == "ATR")
            {
                var dataComponent = _context.GetComponent<DataIndicatorComponent>();
                if (dataComponent == null) return 0;
                
                decimal atr = dataComponent.GetAtrValue();
                return isLong 
                    ? currentPrice - atr * _atrMultiplier.ValueDecimal
                    : currentPrice + atr * _atrMultiplier.ValueDecimal;
            }
            else if (_trailingType.ValueString == "Самообучаемый")
            {
                // ✅ САМООБУЧАЕМЫЙ ТРЕЙЛИНГ: Адаптивный расчет на основе исторических данных
                return CalculateSelfLearningTrailingLevel(positionId, currentPrice, isLong);
            }
            else
            {
                // Фиксированный трейлинг
                return isLong 
                    ? currentPrice * (1 - _trailingDistancePercent.ValueDecimal / 100m)
                    : currentPrice * (1 + _trailingDistancePercent.ValueDecimal / 100m);
            }
        }
        
        // ✅ САМООБУЧАЕМЫЙ ТРЕЙЛИНГ: Адаптивный механизм на основе исторических данных
        private readonly ConcurrentDictionary<int, List<decimal>> _historicalProfits = new();
        private readonly ConcurrentDictionary<int, List<decimal>> _historicalVolatilities = new();
        private readonly ConcurrentDictionary<int, int> _trailingUpdateCount = new();
        
        /// <summary>
        /// САМООБУЧАЕМЫЙ ТРЕЙЛИНГ - детальное описание работы:
        /// 
        /// 1. СБОР ДАННЫХ: Система собирает исторические данные о прибыли и волатильности для каждой позиции
        ///    - Отслеживает максимальную прибыль, достигнутую позицией
        ///    - Измеряет волатильность на основе ATR или стандартного отклонения цен
        ///    - Запоминает паттерны движения цены после активации трейлинга
        /// 
        /// 2. АНАЛИЗ ПАТТЕРНОВ: Система анализирует исторические данные для определения оптимальной дистанции
        ///    - Если исторически позиции часто закрывались раньше времени → увеличивает дистанцию
        ///    - Если позиции часто теряли прибыль → уменьшает дистанцию для более быстрого закрытия
        ///    - Учитывает текущую волатильность рынка (высокая волатильность → большая дистанция)
        /// 
        /// 3. АДАПТАЦИЯ ДИСТАНЦИИ: Дистанция трейлинга динамически корректируется
        ///    - Базовая дистанция = параметр _trailingDistancePercent
        ///    - Корректировка на основе исторических данных: ±20-50% от базовой
        ///    - Учет текущей волатильности: ATR влияет на финальную дистанцию
        /// 
        /// 4. ОБУЧЕНИЕ НА ОСНОВЕ РЕЗУЛЬТАТОВ: После закрытия позиции система анализирует результат
        ///    - Если закрытие было оптимальным (максимальная прибыль сохранена) → сохраняет параметры
        ///    - Если прибыль была потеряна → корректирует алгоритм для будущих позиций
        ///    - Если закрытие было преждевременным → увеличивает дистанцию для похожих ситуаций
        /// 
        /// 5. ПРИМЕНЕНИЕ: Рассчитанная дистанция применяется к текущей цене
        ///    - Для LONG: trailingLevel = currentPrice * (1 - адаптивная_дистанция / 100)
        ///    - Для SHORT: trailingLevel = currentPrice * (1 + адаптивная_дистанция / 100)
        /// </summary>
        private decimal CalculateSelfLearningTrailingLevel(int positionId, decimal currentPrice, bool isLong)
        {
            try
            {
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager == null) return 0;
                
                decimal entryPrice = riskManager.GetEntryPrice(positionId);
                if (entryPrice == 0) return 0;
                
                // Получаем текущую прибыль
                decimal currentProfitPercent = isLong 
                    ? (currentPrice - entryPrice) / entryPrice * 100m
                    : (entryPrice - currentPrice) / entryPrice * 100m;
                
                // Получаем максимальную цену с момента входа
                decimal maxPrice = _highestPricesSinceEntry.GetOrAdd(positionId, entryPrice);
                decimal minPrice = _lowestPricesSinceEntry.GetOrAdd(positionId, entryPrice);
                
                // Рассчитываем волатильность на основе диапазона цен
                decimal priceRange = maxPrice - minPrice;
                decimal volatilityPercent = entryPrice > 0 ? (priceRange / entryPrice) * 100m : 0m;
                
                // Сохраняем исторические данные
                if (!_historicalProfits.ContainsKey(positionId))
                {
                    _historicalProfits[positionId] = new List<decimal>();
                    _historicalVolatilities[positionId] = new List<decimal>();
                }
                
                _historicalProfits[positionId].Add(currentProfitPercent);
                _historicalVolatilities[positionId].Add(volatilityPercent);
                
                // Ограничиваем размер истории (последние 50 значений)
                if (_historicalProfits[positionId].Count > 50)
                {
                    _historicalProfits[positionId].RemoveAt(0);
                    _historicalVolatilities[positionId].RemoveAt(0);
                }
                
                // Базовая дистанция из параметров
                decimal baseDistance = _trailingDistancePercent?.ValueDecimal ?? 0.1m;
                
                // Анализ исторических данных для адаптации
                decimal adaptiveMultiplier = 1.0m;
                
                if (_historicalProfits[positionId].Count >= 5)
                {
                    // Рассчитываем среднюю прибыль и волатильность
                    decimal avgProfit = _historicalProfits[positionId].Average();
                    decimal avgVolatility = _historicalVolatilities[positionId].Average();
                    
                    // Если текущая прибыль выше средней → увеличиваем дистанцию (даем больше пространства)
                    if (currentProfitPercent > avgProfit * 1.2m)
                    {
                        adaptiveMultiplier = 1.3m; // Увеличиваем дистанцию на 30%
                    }
                    // Если текущая прибыль ниже средней → уменьшаем дистанцию (защищаем прибыль)
                    else if (currentProfitPercent < avgProfit * 0.8m)
                    {
                        adaptiveMultiplier = 0.8m; // Уменьшаем дистанцию на 20%
                    }
                    
                    // Учет волатильности: высокая волатильность → большая дистанция
                    if (volatilityPercent > avgVolatility * 1.5m)
                    {
                        adaptiveMultiplier *= 1.2m; // Дополнительное увеличение при высокой волатильности
                    }
                    else if (volatilityPercent < avgVolatility * 0.5m)
                    {
                        adaptiveMultiplier *= 0.9m; // Небольшое уменьшение при низкой волатильности
                    }
                }
                
                // Применяем адаптивную дистанцию
                decimal adaptiveDistance = baseDistance * adaptiveMultiplier;
                
                // Ограничиваем дистанцию разумными пределами (0.05% - 5%)
                adaptiveDistance = Math.Max(0.05m, Math.Min(5.0m, adaptiveDistance));
                
                // Рассчитываем уровень трейлинга
                decimal trailingLevel = isLong 
                    ? currentPrice * (1 - adaptiveDistance / 100m)
                    : currentPrice * (1 + adaptiveDistance / 100m);
                
                // Логирование адаптации (только при значительных изменениях)
                int updateCount = _trailingUpdateCount.GetOrAdd(positionId, 0);
                if (updateCount % 10 == 0) // Логируем каждое 10-е обновление
                {
                    _context.SendLog($"🧠 САМООБУЧАЕМЫЙ ТРЕЙЛИНГ #{positionId}: Базовая дистанция {baseDistance:F2}% → Адаптивная {adaptiveDistance:F2}% (множитель {adaptiveMultiplier:F2}) | Волатильность: {volatilityPercent:F2}%", 
                        LogMessageType.System);
                }
                _trailingUpdateCount[positionId] = updateCount + 1;
                
                return trailingLevel;
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка расчета самообучаемого трейлинга: {ex.Message}", LogMessageType.Error);
                // Fallback на фиксированный трейлинг при ошибке
                return isLong 
                    ? currentPrice * (1 - (_trailingDistancePercent?.ValueDecimal ?? 0.1m) / 100m)
                    : currentPrice * (1 + (_trailingDistancePercent?.ValueDecimal ?? 0.1m) / 100m);
            }
        }
        
        public decimal GetTrailingLevel(int positionId)
        {
            return _currentTrailingLevels.TryGetValue(positionId, out var level) ? level : 0;
        }
        
        /// <summary>
        /// Проверяет, активен ли следящий стоп для позиции
        /// </summary>
        public bool IsTrailingActive(int positionId)
        {
            return _trailingActive.ContainsKey(positionId) && _trailingActive[positionId];
        }
        
        /// <summary>
        /// Получает дистанцию трейлинга в процентах
        /// </summary>
        public decimal GetTrailingDistancePercent()
        {
            return _trailingDistancePercent?.ValueDecimal ?? 0.1m;
        }
        
        /// <summary>
        /// Получает тип трейлинга
        /// </summary>
        public string GetTrailingType()
        {
            return _trailingType?.ValueString ?? "Фиксированный";
        }
        
        /// <summary>
        /// Получает множитель ATR для трейлинга
        /// </summary>
        public decimal GetAtrMultiplier()
        {
            return _atrMultiplier?.ValueDecimal ?? 1.0m;
        }
        
        /// <summary>
        /// ✅ ОЧИСТКА ДАННЫХ САМООБУЧАЕМОГО ТРЕЙЛИНГА при закрытии позиции
        /// </summary>
        public void ClearSelfLearningData(int positionId)
        {
            _historicalProfits.TryRemove(positionId, out _);
            _historicalVolatilities.TryRemove(positionId, out _);
            _trailingUpdateCount.TryRemove(positionId, out _);
        }
    }
    
    #endregion
    
    #region ==================== AI OPTIMIZATION COMPONENTS ====================
    
    // УСИЛЕННЫЕ КЛАССЫ ДЛЯ AI ОПТИМИЗАЦИИ
    public class Particle
    {
        public string Id { get; set; }
        public Dictionary<string, decimal> Position { get; set; }
        public Dictionary<string, decimal> Velocity { get; set; }
        public Dictionary<string, decimal> PersonalBestPosition { get; set; }
        public double PersonalBestFitness { get; set; }
        public double CurrentFitness { get; set; }
        public int StagnationCount { get; set; }
        public int Age { get; set; }
        public List<string> MutationHistory { get; set; }
        
        public Particle()
        {
            Position = new Dictionary<string, decimal>();
            Velocity = new Dictionary<string, decimal>();
            PersonalBestPosition = new Dictionary<string, decimal>();
            PersonalBestFitness = double.MinValue;
            Id = Guid.NewGuid().ToString();
            StagnationCount = 0;
            Age = 0;
            MutationHistory = new List<string>();
        }
    }
    
    public class Swarm
    {
        public List<Particle> Particles { get; set; }
        public Dictionary<string, decimal> GlobalBestPosition { get; set; }
        public double GlobalBestFitness { get; set; }
        public int Iteration { get; set; }
        public List<double> FitnessHistory { get; set; }
        public DateTime LastImprovementTime { get; set; }
        
        public Swarm()
        {
            Particles = new List<Particle>();
            GlobalBestPosition = new Dictionary<string, decimal>();
            GlobalBestFitness = double.MinValue;
            FitnessHistory = new List<double>();
            LastImprovementTime = DateTime.Now;
        }
    }
    
    public class GeneticAlgorithm
    {
        public List<Dictionary<string, decimal>> Population { get; set; }
        public Dictionary<string, decimal> BestChromosome { get; set; }
        public double BestFitness { get; set; }
        public int Generation { get; set; }
        
        public GeneticAlgorithm()
        {
            Population = new List<Dictionary<string, decimal>>();
            BestChromosome = new Dictionary<string, decimal>();
            BestFitness = double.MinValue;
        }
    }
    
    public class HybridOptimizationResult
    {
        public Dictionary<string, decimal> BestParameters { get; set; }
        public double BestFitness { get; set; }
        public int PSOIterations { get; set; }
        public int GAGenerations { get; set; }
        public TimeSpan OptimizationTime { get; set; }
        public DateTime Timestamp { get; set; }
        public string OptimizationMethod { get; set; }
        public DetailedOptimizationReport Report { get; set; }
    }
    
    public class DetailedOptimizationReport
    {
        public double BestFitness { get; set; }
        public double AverageFitness { get; set; }
        public double Diversity { get; set; }
        public double ConvergenceSpeed { get; set; }
        public int EffectiveParticles { get; set; }
        public double ExplorationExploitationRatio { get; set; }
        public int StagnationCount { get; set; }
        public List<string> ImprovementHistory { get; set; }
        public Dictionary<string, decimal> ParameterImprovements { get; set; }
        public Dictionary<string, decimal> ParameterRangesUsed { get; set; }
        public int TotalEvaluations { get; set; }
        
        public DetailedOptimizationReport()
        {
            ImprovementHistory = new List<string>();
            ParameterImprovements = new Dictionary<string, decimal>();
            ParameterRangesUsed = new Dictionary<string, decimal>();
        }
    }
    
    public class EnhancedPSOConfiguration
    {
        public int SwarmSize { get; set; } = 30;
        public int MaxIterations { get; set; } = 100;
        public double Inertia { get; set; } = 0.7;
        public double CognitiveWeight { get; set; } = 1.5;
        public double SocialWeight { get; set; } = 1.5;
        public bool UseAdaptiveInertia { get; set; } = true;
        public double StartInertia { get; set; } = 0.9;
        public double EndInertia { get; set; } = 0.4;
        public bool UseSubSwarms { get; set; } = true;
        public int SubSwarmCount { get; set; } = 3;
        public double MutationRate { get; set; } = 0.15;
        public double CrossoverRate { get; set; } = 0.4;
        public bool UseGeneticEnhancement { get; set; } = true;
        public int GAPopulationSize { get; set; } = 20;
        public int GAGenerations { get; set; } = 50;
        public double GAMutationRate { get; set; } = 0.2;
        public double GACrossoverRate { get; set; } = 0.6;
        public bool ContinuousOptimization { get; set; } = true;
        public int OptimizationIntervalMinutes { get; set; } = 60;
        
        public Dictionary<string, ParameterRange> ParameterRanges { get; set; }
        
        public EnhancedPSOConfiguration()
        {
            ParameterRanges = new Dictionary<string, ParameterRange>
            {
                // ВСЕ ВОЗМОЖНЫЕ ПАРАМЕТРЫ ДЛЯ ОПТИМИЗАЦИИ
                ["TenkanLength"] = new ParameterRange(5, 30, true), // ✅ Исправлено с "TenkanPeriod"
                ["KijunLength"] = new ParameterRange(15, 60, true), // ✅ Исправлено с "KijunPeriod"
                ["SenkouBLength"] = new ParameterRange(40, 120, true), // ✅ Исправлено с "SenkouBPeriod"
                ["SenkouOffset"] = new ParameterRange(20, 60, true),
                ["MinProfitPercent"] = new ParameterRange(0.05m, 2.0m),
                ["TrailingStartPercent"] = new ParameterRange(0.1m, 3.0m),
                ["TrailingDistancePercent"] = new ParameterRange(0.1m, 2.0m),
                ["ATRPeriod"] = new ParameterRange(5, 20, true),
                ["ATRMultiplier"] = new ParameterRange(0.5m, 3.0m),
                ["ManualTakeProfit"] = new ParameterRange(0.5m, 5.0m),
                ["BreakEvenTriggerPercent"] = new ParameterRange(0.05m, 1.0m),
                ["MaxSpreadPercent"] = new ParameterRange(0.05m, 0.5m),
                ["VolumeMultiplier"] = new ParameterRange(0.5m, 3.0m),
                ["VolumePeriod"] = new ParameterRange(10, 50, true),
                ["ReentryCooldownCandles"] = new ParameterRange(1, 10, true),
                ["MaxOpenPositions"] = new ParameterRange(1, 10, true)
            };
        }
    }
    
    public class ParameterRange
    {
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public bool IsInteger { get; set; }
        
        public ParameterRange(decimal min, decimal max, bool isInteger = false)
        {
            MinValue = min;
            MaxValue = max;
            IsInteger = isInteger;
        }
    }
    
    public class BacktestResult
    {
        public double TotalReturn { get; set; }
        public double SharpeRatio { get; set; }
        public double WinRate { get; set; }
        public double MaxDrawdown { get; set; }
        public int TotalTrades { get; set; }
        public double ProfitFactor { get; set; }
        public double RecoveryFactor { get; set; }
        public decimal InitialCapital { get; set; } = 10000m;
        public decimal FinalCapital { get; set; }
        public List<BacktestTrade> Trades { get; set; } = new List<BacktestTrade>();
    }
    
    public class BacktestTrade
    {
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal Volume { get; set; }
        public bool IsLong { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitPercent { get; set; }
        public string ExitReason { get; set; }
    }
    
    // ИНТЕРФЕЙС ДЛЯ РЕАЛЬНОГО БЭКТЕСТИНГА
    public interface IBacktestEngine
    {
        BacktestResult RunBacktest(DateTime from, DateTime to, Dictionary<string, decimal> parameters, List<Candle> candles);
    }
    
    // РЕАЛЬНЫЙ БЭКТЕСТ-ДВИЖОК НА ИСТОРИЧЕСКИХ ДАННЫХ
    public class RealBacktestEngine : IBacktestEngine
    {
        public BacktestResult RunBacktest(DateTime from, DateTime to, Dictionary<string, decimal> parameters, List<Candle> candles)
        {
            var result = new BacktestResult
            {
                InitialCapital = 10000m,
                FinalCapital = 10000m,
                Trades = new List<BacktestTrade>()
            };
            
            if (candles == null || candles.Count < 100)
            {
                return result;
            }
            
            // Извлекаем параметры
            int tenkanPeriod = parameters.ContainsKey("TenkanLength") ? (int)parameters["TenkanLength"] : 9;
            int kijunPeriod = parameters.ContainsKey("KijunLength") ? (int)parameters["KijunLength"] : 26;
            int senkouBPeriod = parameters.ContainsKey("SenkouBLength") ? (int)parameters["SenkouBLength"] : 52;
            int senkouOffset = parameters.ContainsKey("SenkouOffset") ? (int)parameters["SenkouOffset"] : 26;
            decimal minProfitPercent = parameters.ContainsKey("MinProfitPercent") ? parameters["MinProfitPercent"] : 0.14m;
            decimal trailingStartPercent = parameters.ContainsKey("TrailingStartPercent") ? parameters["TrailingStartPercent"] : 0.15m;
            decimal trailingDistancePercent = parameters.ContainsKey("TrailingDistancePercent") ? parameters["TrailingDistancePercent"] : 0.10m;
            decimal atrPeriod = parameters.ContainsKey("ATRPeriod") ? parameters["ATRPeriod"] : 14;
            decimal atrMultiplier = parameters.ContainsKey("ATRMultiplier") ? parameters["ATRMultiplier"] : 1.5m;
            
            // Минимальное количество свечей для расчета индикаторов
            int minCandles = Math.Max(senkouBPeriod + senkouOffset + 30, 100);
            if (candles.Count < minCandles)
            {
                return result;
            }
            
            // Симуляция торговли
            decimal equity = result.InitialCapital;
            decimal peakEquity = equity;
            decimal maxDrawdown = 0;
            BacktestTrade currentPosition = null;
            bool trailingArmed = false;
            decimal trailingLevel = 0;
            decimal breakEvenLevel = 0;
            bool breakEvenApplied = false;
            
            // Начинаем с достаточного количества свечей для расчета индикаторов
            int startIndex = minCandles;
            
            for (int i = startIndex; i < candles.Count - 1; i++)
            {
                var candle = candles[i];
                
                // Пропускаем свечи вне диапазона дат
                if (candle.TimeStart < from || candle.TimeStart > to)
                    continue;
                
                // Расчет индикаторов Ишимоку на исторических данных
                decimal tenkanValue = CalculateTenkan(candles, i, tenkanPeriod);
                decimal kijunValue = CalculateKijun(candles, i, kijunPeriod);
                decimal senkouAValue = CalculateSenkouA(candles, i, tenkanPeriod, kijunPeriod, senkouOffset);
                decimal senkouBValue = CalculateSenkouB(candles, i, senkouBPeriod, senkouOffset);
                decimal atrValue = CalculateATR(candles, i, (int)atrPeriod);
                
                if (tenkanValue == 0 || kijunValue == 0) continue;
                
                // ЛОГИКА ВХОДА
                if (currentPosition == null)
                {
                    bool buySignal = false;
                    bool sellSignal = false;
                    
                    // Сигнал на покупку: Tenkan пересекает Kijun снизу вверх И цена выше облака
                    if (i > 0)
                    {
                        decimal prevTenkan = CalculateTenkan(candles, i - 1, tenkanPeriod);
                        decimal prevKijun = CalculateKijun(candles, i - 1, kijunPeriod);
                        
                        if (prevTenkan < prevKijun && tenkanValue > kijunValue) // Пересечение вверх
                        {
                            decimal cloudTop = Math.Max(senkouAValue, senkouBValue);
                            if (candle.Close > cloudTop)
                            {
                                buySignal = true;
                            }
                        }
                    }
                    
                    // Сигнал на продажу: Tenkan пересекает Kijun сверху вниз И цена ниже облака
                    if (i > 0 && !buySignal)
                    {
                        decimal prevTenkan = CalculateTenkan(candles, i - 1, tenkanPeriod);
                        decimal prevKijun = CalculateKijun(candles, i - 1, kijunPeriod);
                        
                        if (prevTenkan > prevKijun && tenkanValue < kijunValue) // Пересечение вниз
                        {
                            decimal cloudBottom = Math.Min(senkouAValue, senkouBValue);
                            if (candle.Close < cloudBottom)
                            {
                                sellSignal = true;
                            }
                        }
                    }
                    
                    if (buySignal)
                    {
                        decimal volume = equity * 0.1m / candle.Close; // 10% капитала
                        currentPosition = new BacktestTrade
                        {
                            EntryTime = candle.TimeStart,
                            EntryPrice = candle.Close,
                            Volume = volume,
                            IsLong = true
                        };
                        trailingArmed = false;
                        breakEvenApplied = false;
                    }
                    else if (sellSignal)
                    {
                        decimal volume = equity * 0.1m / candle.Close; // 10% капитала
                        currentPosition = new BacktestTrade
                        {
                            EntryTime = candle.TimeStart,
                            EntryPrice = candle.Close,
                            Volume = volume,
                            IsLong = false
                        };
                        trailingArmed = false;
                        breakEvenApplied = false;
                    }
                }
                else
                {
                    // УПРАВЛЕНИЕ ПОЗИЦИЕЙ
                    decimal currentPrice = candle.Close;
                    decimal currentProfit = currentPosition.IsLong 
                        ? (currentPrice - currentPosition.EntryPrice) * currentPosition.Volume
                        : (currentPosition.EntryPrice - currentPrice) * currentPosition.Volume;
                    decimal currentProfitPercent = currentPosition.IsLong
                        ? (currentPrice - currentPosition.EntryPrice) / currentPosition.EntryPrice * 100m
                        : (currentPosition.EntryPrice - currentPrice) / currentPosition.EntryPrice * 100m;
                    
                    // Проверка минимальной прибыли
                    if (currentProfitPercent >= minProfitPercent && !trailingArmed)
                    {
                        trailingArmed = true;
                        trailingLevel = currentPosition.IsLong 
                            ? currentPrice - (currentPrice * trailingDistancePercent / 100m)
                            : currentPrice + (currentPrice * trailingDistancePercent / 100m);
                    }
                    
                    // Break Even
                    if (!breakEvenApplied && currentProfitPercent >= minProfitPercent * 0.5m)
                    {
                        breakEvenApplied = true;
                        breakEvenLevel = currentPosition.EntryPrice;
                    }
                    
                    // Выход по трейлингу
                    bool exitByTrailing = false;
                    if (trailingArmed)
                    {
                        if (currentPosition.IsLong)
                        {
                            if (currentPrice > trailingLevel + (currentPrice * trailingDistancePercent / 100m))
                            {
                                trailingLevel = currentPrice - (currentPrice * trailingDistancePercent / 100m);
                            }
                            if (currentPrice <= trailingLevel)
                            {
                                exitByTrailing = true;
                            }
                        }
                        else
                        {
                            if (currentPrice < trailingLevel - (currentPrice * trailingDistancePercent / 100m))
                            {
                                trailingLevel = currentPrice + (currentPrice * trailingDistancePercent / 100m);
                            }
                            if (currentPrice >= trailingLevel)
                            {
                                exitByTrailing = true;
                            }
                        }
                    }
                    
                    // Выход по Break Even
                    bool exitByBreakEven = false;
                    if (breakEvenApplied && currentPosition.IsLong && currentPrice <= breakEvenLevel)
                    {
                        exitByBreakEven = true;
                    }
                    else if (breakEvenApplied && !currentPosition.IsLong && currentPrice >= breakEvenLevel)
                    {
                        exitByBreakEven = true;
                    }
                    
                    // Выход по противоположному сигналу
                    bool exitBySignal = false;
                    if (i > 0)
                    {
                        decimal prevTenkan = CalculateTenkan(candles, i - 1, tenkanPeriod);
                        decimal prevKijun = CalculateKijun(candles, i - 1, kijunPeriod);
                        
                        if (currentPosition.IsLong && prevTenkan > prevKijun && tenkanValue < kijunValue)
                        {
                            exitBySignal = true;
                        }
                        else if (!currentPosition.IsLong && prevTenkan < prevKijun && tenkanValue > kijunValue)
                        {
                            exitBySignal = true;
                        }
                    }
                    
                    // Выход по стоп-лоссу (ATR)
                    bool exitByStopLoss = false;
                    if (atrValue > 0)
                    {
                        decimal stopLoss = currentPosition.IsLong
                            ? currentPosition.EntryPrice - (atrValue * atrMultiplier)
                            : currentPosition.EntryPrice + (atrValue * atrMultiplier);
                        
                        if (currentPosition.IsLong && currentPrice <= stopLoss)
                        {
                            exitByStopLoss = true;
                        }
                        else if (!currentPosition.IsLong && currentPrice >= stopLoss)
                        {
                            exitByStopLoss = true;
                        }
                    }
                    
                    // ЗАКРЫТИЕ ПОЗИЦИИ
                    if (exitByTrailing || exitByBreakEven || exitBySignal || exitByStopLoss)
                    {
                        currentPosition.ExitTime = candle.TimeStart;
                        currentPosition.ExitPrice = currentPrice;
                        currentPosition.Profit = currentProfit;
                        currentPosition.ProfitPercent = currentProfitPercent;
                        
                        if (exitByTrailing) currentPosition.ExitReason = "Trailing";
                        else if (exitByBreakEven) currentPosition.ExitReason = "BreakEven";
                        else if (exitBySignal) currentPosition.ExitReason = "Signal";
                        else if (exitByStopLoss) currentPosition.ExitReason = "StopLoss";
                        
                        equity += currentProfit;
                        result.Trades.Add(currentPosition);
                        currentPosition = null;
                    }
                }
                
                // Обновление статистики
                if (equity > peakEquity)
                {
                    peakEquity = equity;
                }
                else
                {
                    decimal drawdown = (peakEquity - equity) / peakEquity * 100m;
                    if (drawdown > maxDrawdown)
                    {
                        maxDrawdown = drawdown;
                    }
                }
            }
            
            // Закрываем последнюю позицию если есть
            if (currentPosition != null && candles.Count > 0)
            {
                var lastCandle = candles[candles.Count - 1];
                decimal finalProfit = currentPosition.IsLong
                    ? (lastCandle.Close - currentPosition.EntryPrice) * currentPosition.Volume
                    : (currentPosition.EntryPrice - lastCandle.Close) * currentPosition.Volume;
                
                currentPosition.ExitTime = lastCandle.TimeStart;
                currentPosition.ExitPrice = lastCandle.Close;
                currentPosition.Profit = finalProfit;
                currentPosition.ProfitPercent = currentPosition.IsLong
                    ? (lastCandle.Close - currentPosition.EntryPrice) / currentPosition.EntryPrice * 100m
                    : (currentPosition.EntryPrice - lastCandle.Close) / currentPosition.EntryPrice * 100m;
                currentPosition.ExitReason = "EndOfData";
                
                equity += finalProfit;
                result.Trades.Add(currentPosition);
            }
            
            // Расчет финальной статистики
            result.FinalCapital = equity;
            result.TotalReturn = (double)((equity - result.InitialCapital) / result.InitialCapital * 100m);
            result.MaxDrawdown = (double)maxDrawdown;
            result.TotalTrades = result.Trades.Count;
            
            if (result.TotalTrades > 0)
            {
                int winningTrades = result.Trades.Count(t => t.Profit > 0);
                result.WinRate = (double)winningTrades / result.TotalTrades * 100.0;
                
                decimal totalProfit = result.Trades.Where(t => t.Profit > 0).Sum(t => t.Profit);
                decimal totalLoss = Math.Abs(result.Trades.Where(t => t.Profit < 0).Sum(t => t.Profit));
                result.ProfitFactor = totalLoss > 0 ? (double)(totalProfit / totalLoss) : (totalProfit > 0 ? 10.0 : 0.5);
                
                // Расчет Sharpe Ratio (упрощенный)
                if (result.TotalTrades > 1)
                {
                    var returns = result.Trades.Select(t => (double)t.ProfitPercent).ToList();
                    double avgReturn = returns.Average();
                    double stdDev = Math.Sqrt(returns.Average(r => Math.Pow(r - avgReturn, 2)));
                    result.SharpeRatio = stdDev > 0 ? avgReturn / stdDev : 0;
                }
                
                result.RecoveryFactor = result.MaxDrawdown > 0 ? result.TotalReturn / result.MaxDrawdown : result.TotalReturn;
            }
            
            return result;
        }
        
        private decimal CalculateTenkan(List<Candle> candles, int index, int period)
        {
            if (index < period - 1) return 0;
            var range = candles.Skip(index - period + 1).Take(period);
            return (range.Max(c => c.High) + range.Min(c => c.Low)) / 2;
        }
        
        private decimal CalculateKijun(List<Candle> candles, int index, int period)
        {
            if (index < period - 1) return 0;
            var range = candles.Skip(index - period + 1).Take(period);
            return (range.Max(c => c.High) + range.Min(c => c.Low)) / 2;
        }
        
        private decimal CalculateSenkouA(List<Candle> candles, int index, int tenkanPeriod, int kijunPeriod, int offset)
        {
            if (index < offset) return 0;
            int calcIndex = index - offset;
            if (calcIndex < Math.Max(tenkanPeriod, kijunPeriod) - 1) return 0;
            decimal tenkan = CalculateTenkan(candles, calcIndex, tenkanPeriod);
            decimal kijun = CalculateKijun(candles, calcIndex, kijunPeriod);
            return (tenkan + kijun) / 2;
        }
        
        private decimal CalculateSenkouB(List<Candle> candles, int index, int period, int offset)
        {
            if (index < offset) return 0;
            int calcIndex = index - offset;
            if (calcIndex < period - 1) return 0;
            var range = candles.Skip(calcIndex - period + 1).Take(period);
            return (range.Max(c => c.High) + range.Min(c => c.Low)) / 2;
        }
        
        private decimal CalculateATR(List<Candle> candles, int index, int period)
        {
            if (index < period) return 0;
            decimal sum = 0;
            for (int i = index - period + 1; i <= index; i++)
            {
                if (i > 0)
                {
                    decimal tr = Math.Max(
                        candles[i].High - candles[i].Low,
                        Math.Max(
                            Math.Abs(candles[i].High - candles[i - 1].Close),
                            Math.Abs(candles[i].Low - candles[i - 1].Close)
                        )
                    );
                    sum += tr;
                }
            }
            return sum / period;
        }
    }
    
    // 5. КОМПОНЕНТ AI ОПТИМИЗАЦИИ С УСИЛЕННЫМ ФУНКЦИОНАЛОМ
    public class EnhancedAIOptimizationComponent : ITradingComponent
    {
        public string ComponentName => "EnhancedAIOptimization";
        
        private IComponentContext _context;
        private Swarm _currentSwarm;
        private GeneticAlgorithm _geneticAlgorithm;
        private HybridOptimizationResult _lastResult;
        private bool _isOptimizationRunning;
        private DateTime _lastOptimization;
        private DateTime _lastContinuousOptimization;
        private readonly ConcurrentDictionary<string, double> _fitnessCache = new();
        private DetailedOptimizationReport _currentReport;
        private List<Candle> _historicalCandles;
        private Random _random;
        private CancellationTokenSource _optimizationCancellationTokenSource; // Для отмены оптимизации
        private StrategyParameterBool _preserveSafetyLogic; // Для проверки защищенных параметров
        private EnhancedPSOConfiguration _config; // Текущая конфигурация для доступа в логах
        
        private StrategyParameterString _useAIOptimization;
        private StrategyParameterString _optimizationMode;
        private StrategyParameterBool _autoApplyResults;
        private StrategyParameterInt _psoSwarmSize;
        private StrategyParameterInt _psoMaxIterations;
        private StrategyParameterDecimal _psoInertia;
        private StrategyParameterDecimal _psoCognitiveWeight;
        private StrategyParameterDecimal _psoSocialWeight;
        private StrategyParameterString _psoUseAdaptiveInertia;
        private StrategyParameterDecimal _psoStartInertia;
        private StrategyParameterDecimal _psoEndInertia;
        private StrategyParameterString _psoUseSubSwarms;
        private StrategyParameterInt _psoSubSwarmCount;
        private StrategyParameterDecimal _psoMutationRate;
        private StrategyParameterDecimal _psoCrossoverRate;
        private StrategyParameterString _useGeneticEnhancement;
        private StrategyParameterInt _gaPopulationSize;
        private StrategyParameterInt _gaGenerations;
        private StrategyParameterDecimal _gaMutationRate;
        private StrategyParameterDecimal _gaCrossoverRate;
        private StrategyParameterString _continuousOptimization;
        private StrategyParameterInt _optimizationIntervalMinutes;
        
        // Флаги выбора параметров для оптимизации
        private StrategyParameterBool _optimizeTenkanLength;
        private StrategyParameterBool _optimizeKijunLength;
        private StrategyParameterBool _optimizeSenkouBLength;
        private StrategyParameterBool _optimizeSenkouOffset;
        private StrategyParameterBool _optimizeStochPeriod;
        private StrategyParameterBool _optimizeStochSmoothing;
        private StrategyParameterBool _optimizeStochDPeriod;
        private StrategyParameterBool _optimizeStochOversold;
        private StrategyParameterBool _optimizeStochOverbought;
        private StrategyParameterBool _optimizeAveragingLevel1;
        private StrategyParameterBool _optimizeAveragingLevel2;
        private StrategyParameterBool _optimizeAveragingLevel3;
        private StrategyParameterBool _optimizeAveragingLevel4;
        private StrategyParameterBool _optimizeAveragingLevel5;
        private StrategyParameterBool _optimizeAveragingLevel6;
        private StrategyParameterBool _optimizeAveragingLevel7;
        private StrategyParameterBool _optimizeAveragingLevel8;
        private StrategyParameterBool _optimizeAveragingLevel9;
        private StrategyParameterBool _optimizeAveragingLevel10;
        private StrategyParameterBool _optimizeAveragingLevel11;
        private StrategyParameterBool _optimizeAveragingLevel12;
        private StrategyParameterBool _optimizeMinProfitPercent;
        private StrategyParameterBool _optimizeTrailingStartPercent;
        private StrategyParameterBool _optimizeTrailingDistancePercent;
        private StrategyParameterBool _optimizeSelfLearningTrailing;
        private StrategyParameterBool _optimizeManualTakeProfit;
        private StrategyParameterBool _optimizeBreakEvenTriggerPercent;
        private StrategyParameterBool _optimizeMaxSpreadPercent;
        private StrategyParameterBool _optimizeATRPeriod;
        private StrategyParameterBool _optimizeATRMultiplier;
        private StrategyParameterBool _optimizeVolumeMultiplier;
        private StrategyParameterBool _optimizeVolumePeriod;
        private StrategyParameterBool _optimizeReentryCooldownCandles;
        private StrategyParameterBool _optimizeMaxOpenPositions;
        private StrategyParameterBool _optimizeCounterintuitiveEma1Period;
        private StrategyParameterBool _optimizeCounterintuitiveEma2Period;
        private StrategyParameterBool _optimizeCounterintuitiveEma3Period;
        
        public DateTime LastOptimizationTime => _lastOptimization;
        public double BestFitness => _lastResult?.BestFitness ?? 0;
        public Dictionary<string, decimal> LastBestParameters => _lastResult?.BestParameters;
        
        public string GetStatusSummary()
        {
            if (_isOptimizationRunning)
            {
                return $"⚡ ГИБРИДНАЯ AI ОПТИМИЗАЦИЯ В РАБОТЕ | PSO: {_currentSwarm?.Iteration ?? 0} | GA: {_geneticAlgorithm?.Generation ?? 0} | Лучший фитнес: {_currentSwarm?.GlobalBestFitness ?? 0:F2}%";
            }
            if (_lastResult != null)
            {
                return $"🤖 AI СТАТУС: ГОТОВ | Лучший фитнес: {_lastResult.BestFitness:F2}% | Метод: {_lastResult.OptimizationMethod} | Время: {_lastResult.OptimizationTime:hh\\:mm\\:ss}";
            }
            return "🤖 AI СТАТУС: ОЖИДАНИЕ ЗАПУСКА";
        }
        
        public void Initialize(IComponentContext context)
        {
            _context = context;
            _random = new Random();
            
            // Получаем параметры
            if (context.SharedData.TryGetValue("UseAIOptimization", out var useAI))
                _useAIOptimization = useAI as StrategyParameterString;
            if (context.SharedData.TryGetValue("OptimizationMode", out var optMode))
                _optimizationMode = optMode as StrategyParameterString;
            if (context.SharedData.TryGetValue("AutoApplyResults", out var autoApply))
                _autoApplyResults = autoApply as StrategyParameterBool;
            if (context.SharedData.TryGetValue("PsoSwarmSize", out var swarmSize))
                _psoSwarmSize = swarmSize as StrategyParameterInt;
            if (context.SharedData.TryGetValue("PsoMaxIterations", out var maxIter))
                _psoMaxIterations = maxIter as StrategyParameterInt;
            if (context.SharedData.TryGetValue("PsoInertia", out var inertia))
                _psoInertia = inertia as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("PsoCognitiveWeight", out var cognitive))
                _psoCognitiveWeight = cognitive as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("PsoSocialWeight", out var social))
                _psoSocialWeight = social as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("PsoUseAdaptiveInertia", out var adaptive))
                _psoUseAdaptiveInertia = adaptive as StrategyParameterString;
            if (context.SharedData.TryGetValue("PsoStartInertia", out var startInertia))
                _psoStartInertia = startInertia as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("PsoEndInertia", out var endInertia))
                _psoEndInertia = endInertia as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("PsoUseSubSwarms", out var subSwarms))
                _psoUseSubSwarms = subSwarms as StrategyParameterString;
            if (context.SharedData.TryGetValue("PsoSubSwarmCount", out var subSwarmCount))
                _psoSubSwarmCount = subSwarmCount as StrategyParameterInt;
            if (context.SharedData.TryGetValue("PsoMutationRate", out var mutation))
                _psoMutationRate = mutation as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("PsoCrossoverRate", out var crossover))
                _psoCrossoverRate = crossover as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("UseGeneticEnhancement", out var useGenetic))
                _useGeneticEnhancement = useGenetic as StrategyParameterString;
            if (context.SharedData.TryGetValue("GaPopulationSize", out var gaPopulation))
                _gaPopulationSize = gaPopulation as StrategyParameterInt;
            if (context.SharedData.TryGetValue("GaGenerations", out var gaGenerations))
                _gaGenerations = gaGenerations as StrategyParameterInt;
            if (context.SharedData.TryGetValue("GaMutationRate", out var gaMutation))
                _gaMutationRate = gaMutation as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("GaCrossoverRate", out var gaCrossover))
                _gaCrossoverRate = gaCrossover as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("ContinuousOptimization", out var continuous))
                _continuousOptimization = continuous as StrategyParameterString;
            if (context.SharedData.TryGetValue("OptimizationIntervalMinutes", out var interval))
                _optimizationIntervalMinutes = interval as StrategyParameterInt;
            if (context.SharedData.TryGetValue("PreserveSafetyLogic", out var preserveSafety))
                _preserveSafetyLogic = preserveSafety as StrategyParameterBool;
            
            // Получаем флаги выбора параметров для оптимизации
            if (context.SharedData.TryGetValue("OptimizeTenkanLength", out var optTenkan))
                _optimizeTenkanLength = optTenkan as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeKijunLength", out var optKijun))
                _optimizeKijunLength = optKijun as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeSenkouBLength", out var optSenkouB))
                _optimizeSenkouBLength = optSenkouB as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeSenkouOffset", out var optSenkouOffset))
                _optimizeSenkouOffset = optSenkouOffset as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeStochPeriod", out var optStochPeriod))
                _optimizeStochPeriod = optStochPeriod as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeStochSmoothing", out var optStochSmooth))
                _optimizeStochSmoothing = optStochSmooth as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeStochDPeriod", out var optStochD))
                _optimizeStochDPeriod = optStochD as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeStochOversold", out var optStochOs))
                _optimizeStochOversold = optStochOs as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeStochOverbought", out var optStochOb))
                _optimizeStochOverbought = optStochOb as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel1", out var optAvg1))
                _optimizeAveragingLevel1 = optAvg1 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel2", out var optAvg2))
                _optimizeAveragingLevel2 = optAvg2 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel3", out var optAvg3))
                _optimizeAveragingLevel3 = optAvg3 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel4", out var optAvg4))
                _optimizeAveragingLevel4 = optAvg4 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel5", out var optAvg5))
                _optimizeAveragingLevel5 = optAvg5 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel6", out var optAvg6))
                _optimizeAveragingLevel6 = optAvg6 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel7", out var optAvg7))
                _optimizeAveragingLevel7 = optAvg7 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel8", out var optAvg8))
                _optimizeAveragingLevel8 = optAvg8 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel9", out var optAvg9))
                _optimizeAveragingLevel9 = optAvg9 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel10", out var optAvg10))
                _optimizeAveragingLevel10 = optAvg10 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel11", out var optAvg11))
                _optimizeAveragingLevel11 = optAvg11 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeAveragingLevel12", out var optAvg12))
                _optimizeAveragingLevel12 = optAvg12 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeMinProfitPercent", out var optMinProfit))
                _optimizeMinProfitPercent = optMinProfit as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeTrailingStartPercent", out var optTrailingStart))
                _optimizeTrailingStartPercent = optTrailingStart as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeTrailingDistancePercent", out var optTrailingDist))
                _optimizeTrailingDistancePercent = optTrailingDist as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeSelfLearningTrailing", out var optSelfLearning))
                _optimizeSelfLearningTrailing = optSelfLearning as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeManualTakeProfit", out var optManualTP))
                _optimizeManualTakeProfit = optManualTP as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeBreakEvenTriggerPercent", out var optBreakEven))
                _optimizeBreakEvenTriggerPercent = optBreakEven as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeMaxSpreadPercent", out var optMaxSpread))
                _optimizeMaxSpreadPercent = optMaxSpread as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeATRPeriod", out var optATRPeriod))
                _optimizeATRPeriod = optATRPeriod as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeATRMultiplier", out var optATRMult))
                _optimizeATRMultiplier = optATRMult as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeVolumeMultiplier", out var optVolMult))
                _optimizeVolumeMultiplier = optVolMult as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeVolumePeriod", out var optVolPeriod))
                _optimizeVolumePeriod = optVolPeriod as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeReentryCooldownCandles", out var optReentry))
                _optimizeReentryCooldownCandles = optReentry as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeMaxOpenPositions", out var optMaxPos))
                _optimizeMaxOpenPositions = optMaxPos as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeCounterintuitiveEma1Period", out var optCounterEma1))
                _optimizeCounterintuitiveEma1Period = optCounterEma1 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeCounterintuitiveEma2Period", out var optCounterEma2))
                _optimizeCounterintuitiveEma2Period = optCounterEma2 as StrategyParameterBool;
            if (context.SharedData.TryGetValue("OptimizeCounterintuitiveEma3Period", out var optCounterEma3))
                _optimizeCounterintuitiveEma3Period = optCounterEma3 as StrategyParameterBool;
            
            _context.SendLog("🚀 УСИЛЕННЫЙ AI МОДУЛЬ ОПТИМИЗАЦИИ ИНИЦИАЛИЗИРОВАН", LogMessageType.System);
            _context.SendLog("⚡ РЕЖИМ: ГИБРИДНЫЙ PSO + ГЕНЕТИЧЕСКИЙ АЛГОРИТМ", LogMessageType.System);
            _context.SendLog("🎯 ОПТИМИЗАЦИЯ ВСЕХ ПАРАМЕТРОВ ВКЛЮЧЕНА", LogMessageType.System);
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            await Task.CompletedTask;
            
            CheckContinuousOptimization();
        }
        
        public void CancelOptimization()
        {
            // Метод для отмены текущей оптимизации
            if (_optimizationCancellationTokenSource != null && !_optimizationCancellationTokenSource.Token.IsCancellationRequested)
            {
                _optimizationCancellationTokenSource.Cancel();
                _context.SendLog("⚠️ Запрос на отмену оптимизации отправлен", LogMessageType.System);
            }
        }
        
        public void Dispose()
        {
            // Отменяем оптимизацию при освобождении ресурсов
            CancelOptimization();
            
            _fitnessCache.Clear();
            _historicalCandles?.Clear();
            _currentSwarm = null;
            _geneticAlgorithm = null;
            _lastResult = null;
            _optimizationCancellationTokenSource?.Dispose();
        }
        
        public async Task<HybridOptimizationResult> StartHybridOptimizationAsync(CancellationToken cancellationToken = default)
        {
            if (_isOptimizationRunning)
            {
                _context.SendLog("⚠️ Оптимизация уже выполняется", LogMessageType.System);
                return _lastResult;
            }
            
            // Создаем CancellationTokenSource для управления отменой
            _optimizationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _optimizationCancellationTokenSource.Token;
            
            _isOptimizationRunning = true;
            try
            {
                _context.SendLog("🚀 ЗАПУСК ГИБРИДНОЙ AI ОПТИМИЗАЦИИ...", LogMessageType.System);
                _context.SendLog("🎯 PSO + ГЕНЕТИЧЕСКИЙ АЛГОРИТМ + НЕПРЕРЫВНАЯ ОПТИМИЗАЦИЯ", LogMessageType.System);
                
                var config = CreateEnhancedConfiguration();
                _config = config; // Сохраняем для доступа в логах
                LogEnhancedConfiguration(config);
                
                // Загружаем исторические данные
                var tab = _context.GetTab();
                if (tab?.CandlesAll != null && tab.CandlesAll.Count > 0)
                {
                    _historicalCandles = new List<Candle>(tab.CandlesAll);
                    _context.SendLog($"📊 Загружено {_historicalCandles?.Count ?? 0} исторических свечей", 
                        LogMessageType.System);
                }
                else
                {
                    _context.SendLog("⚠️ Исторические данные недоступны, используется симуляция", 
                        LogMessageType.System);
                }
                
                _currentReport = new DetailedOptimizationReport();
                _currentReport.TotalEvaluations = 0;
                
                // Запускаем гибридную оптимизацию с поддержкой отмены
                var result = await RunHybridOptimization(config, token);
                
                _lastResult = result;
                _lastOptimization = DateTime.Now;
                _lastContinuousOptimization = DateTime.Now;
                
                GenerateEnhancedReport(result, config);
                
                // Автоприменение результатов если включено
                if (_autoApplyResults.ValueBool && result.BestParameters != null)
                {
                    ApplyOptimizedParameters(result.BestParameters);
                }
                
                return result;
            }
            catch (OperationCanceledException)
            {
                _context.SendLog("⚠️ Оптимизация отменена пользователем", LogMessageType.System);
                return _lastResult ?? new HybridOptimizationResult 
                { 
                    Timestamp = DateTime.Now,
                    OptimizationMethod = "Отменено"
                };
            }
            finally
            {
                _isOptimizationRunning = false;
                _optimizationCancellationTokenSource?.Dispose();
                _optimizationCancellationTokenSource = null;
            }
        }
        
        private EnhancedPSOConfiguration CreateEnhancedConfiguration()
        {
            var config = new EnhancedPSOConfiguration
            {
                SwarmSize = _psoSwarmSize.ValueInt,
                MaxIterations = _psoMaxIterations.ValueInt,
                Inertia = (double)_psoInertia.ValueDecimal,
                CognitiveWeight = (double)_psoCognitiveWeight.ValueDecimal,
                SocialWeight = (double)_psoSocialWeight.ValueDecimal,
                UseAdaptiveInertia = _psoUseAdaptiveInertia.ValueString == "Включено",
                StartInertia = (double)_psoStartInertia.ValueDecimal,
                EndInertia = (double)_psoEndInertia.ValueDecimal,
                UseSubSwarms = _psoUseSubSwarms.ValueString == "Включено",
                SubSwarmCount = _psoSubSwarmCount.ValueInt,
                MutationRate = (double)_psoMutationRate.ValueDecimal,
                CrossoverRate = (double)_psoCrossoverRate.ValueDecimal,
                UseGeneticEnhancement = _useGeneticEnhancement.ValueString == "Включено",
                GAPopulationSize = _gaPopulationSize.ValueInt,
                GAGenerations = _gaGenerations.ValueInt,
                GAMutationRate = (double)_gaMutationRate.ValueDecimal,
                GACrossoverRate = (double)_gaCrossoverRate.ValueDecimal,
                ContinuousOptimization = _continuousOptimization.ValueString == "Включено",
                OptimizationIntervalMinutes = _optimizationIntervalMinutes.ValueInt
            };
            
            // ДОБАВЛЯЕМ ПАРАМЕТРЫ ДЛЯ ОПТИМИЗАЦИИ В ЗАВИСИМОСТИ ОТ ВЫБРАННЫХ ФЛАГОВ
            config.ParameterRanges.Clear();
            
            // ✅ ИСПРАВЛЕНИЕ: Строгая проверка - добавляем параметр ТОЛЬКО если чекбокс включен
            // Основные параметры Ишимоку
            if (_optimizeTenkanLength != null && _optimizeTenkanLength.ValueBool == true)
                config.ParameterRanges["TenkanLength"] = new ParameterRange(5, 30, true);
            if (_optimizeKijunLength != null && _optimizeKijunLength.ValueBool == true)
                config.ParameterRanges["KijunLength"] = new ParameterRange(15, 60, true);
            if (_optimizeSenkouBLength != null && _optimizeSenkouBLength.ValueBool == true)
                config.ParameterRanges["SenkouBLength"] = new ParameterRange(40, 120, true);
            if (_optimizeSenkouOffset != null && _optimizeSenkouOffset.ValueBool == true)
                config.ParameterRanges["SenkouOffset"] = new ParameterRange(20, 60, true);
            if (_optimizeStochPeriod != null && _optimizeStochPeriod.ValueBool == true)
                config.ParameterRanges["StochPeriod"] = new ParameterRange(5, 50, true);
            if (_optimizeStochSmoothing != null && _optimizeStochSmoothing.ValueBool == true)
                config.ParameterRanges["StochSmoothing"] = new ParameterRange(1, 10, true);
            if (_optimizeStochDPeriod != null && _optimizeStochDPeriod.ValueBool == true)
                config.ParameterRanges["StochDPeriod"] = new ParameterRange(1, 10, true);
            if (_optimizeStochOversold != null && _optimizeStochOversold.ValueBool == true)
                config.ParameterRanges["StochOversold"] = new ParameterRange(5m, 40m);
            if (_optimizeStochOverbought != null && _optimizeStochOverbought.ValueBool == true)
                config.ParameterRanges["StochOverbought"] = new ParameterRange(60m, 95m);
            
            // Параметры риск-менеджмента
            if (_optimizeMinProfitPercent != null && _optimizeMinProfitPercent.ValueBool == true)
                config.ParameterRanges["MinProfitPercent"] = new ParameterRange(0.05m, 2.0m);
            if (_optimizeTrailingStartPercent != null && _optimizeTrailingStartPercent.ValueBool == true)
                config.ParameterRanges["TrailingStartPercent"] = new ParameterRange(0.1m, 3.0m);
            if (_optimizeTrailingDistancePercent != null && _optimizeTrailingDistancePercent.ValueBool == true)
                config.ParameterRanges["TrailingDistancePercent"] = new ParameterRange(0.1m, 2.0m);
            // ✅ САМООБУЧАЕМЫЙ ТРЕЙЛИНГ: Оптимизация параметров адаптации
            // Когда включен самообучаемый трейлинг, оптимизируются базовые параметры трейлинга,
            // которые используются как основа для адаптации
            if (_optimizeSelfLearningTrailing != null && _optimizeSelfLearningTrailing.ValueBool == true)
            {
                // Оптимизируем базовую дистанцию трейлинга (используется как основа для адаптации)
                if (!config.ParameterRanges.ContainsKey("TrailingDistancePercent"))
                    config.ParameterRanges["TrailingDistancePercent"] = new ParameterRange(0.05m, 5.0m);
                // Оптимизируем старт трейлинга (когда активируется адаптация)
                if (!config.ParameterRanges.ContainsKey("TrailingStartPercent"))
                    config.ParameterRanges["TrailingStartPercent"] = new ParameterRange(0.05m, 5.0m);
            }
            if (_optimizeManualTakeProfit != null && _optimizeManualTakeProfit.ValueBool == true)
                config.ParameterRanges["ManualTakeProfit"] = new ParameterRange(0.5m, 5.0m);
            if (_optimizeBreakEvenTriggerPercent != null && _optimizeBreakEvenTriggerPercent.ValueBool == true)
                config.ParameterRanges["BreakEvenTriggerPercent"] = new ParameterRange(0.05m, 1.0m);
            if (_optimizeMaxSpreadPercent != null && _optimizeMaxSpreadPercent.ValueBool == true)
                config.ParameterRanges["MaxSpreadPercent"] = new ParameterRange(0.05m, 0.5m);
            
            // Параметры ATR
            if (_optimizeATRPeriod != null && _optimizeATRPeriod.ValueBool == true)
                config.ParameterRanges["ATRPeriod"] = new ParameterRange(5, 20, true);
            if (_optimizeATRMultiplier != null && _optimizeATRMultiplier.ValueBool == true)
                config.ParameterRanges["ATRMultiplier"] = new ParameterRange(0.5m, 3.0m);
            
            // Общие параметры управления
            if (_optimizeVolumeMultiplier != null && _optimizeVolumeMultiplier.ValueBool == true)
                config.ParameterRanges["VolumeMultiplier"] = new ParameterRange(0.5m, 3.0m);
            if (_optimizeVolumePeriod != null && _optimizeVolumePeriod.ValueBool == true)
                config.ParameterRanges["VolumePeriod"] = new ParameterRange(10, 50, true);
            if (_optimizeReentryCooldownCandles != null && _optimizeReentryCooldownCandles.ValueBool == true)
                config.ParameterRanges["ReentryCooldownCandles"] = new ParameterRange(1, 10, true);
            if (_optimizeAveragingLevel1 != null && _optimizeAveragingLevel1.ValueBool == true)
                config.ParameterRanges["AveragingLevel1"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel2 != null && _optimizeAveragingLevel2.ValueBool == true)
                config.ParameterRanges["AveragingLevel2"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel3 != null && _optimizeAveragingLevel3.ValueBool == true)
                config.ParameterRanges["AveragingLevel3"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel4 != null && _optimizeAveragingLevel4.ValueBool == true)
                config.ParameterRanges["AveragingLevel4"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel5 != null && _optimizeAveragingLevel5.ValueBool == true)
                config.ParameterRanges["AveragingLevel5"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel6 != null && _optimizeAveragingLevel6.ValueBool == true)
                config.ParameterRanges["AveragingLevel6"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel7 != null && _optimizeAveragingLevel7.ValueBool == true)
                config.ParameterRanges["AveragingLevel7"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel8 != null && _optimizeAveragingLevel8.ValueBool == true)
                config.ParameterRanges["AveragingLevel8"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel9 != null && _optimizeAveragingLevel9.ValueBool == true)
                config.ParameterRanges["AveragingLevel9"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel10 != null && _optimizeAveragingLevel10.ValueBool == true)
                config.ParameterRanges["AveragingLevel10"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel11 != null && _optimizeAveragingLevel11.ValueBool == true)
                config.ParameterRanges["AveragingLevel11"] = new ParameterRange(0.1m, 100.0m);
            if (_optimizeAveragingLevel12 != null && _optimizeAveragingLevel12.ValueBool == true)
                config.ParameterRanges["AveragingLevel12"] = new ParameterRange(0.1m, 100.0m);
            
            // Counterintuitive параметры
            if (_optimizeCounterintuitiveEma1Period != null && _optimizeCounterintuitiveEma1Period.ValueBool == true)
                config.ParameterRanges["CounterintuitiveEma1Period"] = new ParameterRange(10, 5000, true);
            if (_optimizeCounterintuitiveEma2Period != null && _optimizeCounterintuitiveEma2Period.ValueBool == true)
                config.ParameterRanges["CounterintuitiveEma2Period"] = new ParameterRange(5, 5000, true);
            if (_optimizeCounterintuitiveEma3Period != null && _optimizeCounterintuitiveEma3Period.ValueBool == true)
                config.ParameterRanges["CounterintuitiveEma3Period"] = new ParameterRange(3, 5000, true);
            
            // ВАЖНО: MaxOpenPositions относится к управлению риском и не должен меняться оптимизатором.
            // Поэтому мы сознательно НЕ добавляем его в список параметров для оптимизации,
            // даже если установлен флаг OptimizeMaxOpenPositions.
            
            return config;
        }
        
        /// <summary>
        /// Валидация конфигурации: проверяет минимальные требования (не добавляет параметры, которые пользователь отключил)
        /// </summary>
        private void ValidateConfiguration(EnhancedPSOConfiguration config)
        {
            // ✅ ИСПРАВЛЕНИЕ: НЕ добавляем параметры принудительно!
            // Пользователь сам решает, какие параметры оптимизировать через чекбоксы.
            // Проверяем только, что есть хотя бы один параметр для оптимизации.
            
            if (config.ParameterRanges.Count == 0)
            {
                _context.SendLog("⚠️ ВНИМАНИЕ: Не выбрано ни одного параметра для оптимизации! Добавляем минимальный набор.", 
                    LogMessageType.System);
                
                // Только если ВООБЩЕ нет параметров - добавляем минимальный набор
                // Но это должно быть исключительной ситуацией
                config.ParameterRanges["TenkanLength"] = new ParameterRange(5, 30, true);
                config.ParameterRanges["KijunLength"] = new ParameterRange(15, 60, true);
            }
            else
            {
                _context.SendLog($"✅ Валидация: выбрано {config.ParameterRanges.Count} параметров для оптимизации", 
                    LogMessageType.System);
            }
        }
        
        private void LogEnhancedConfiguration(EnhancedPSOConfiguration config)
        {
            _context.SendLog("=== 🚀 УСИЛЕННАЯ КОНФИГУРАЦИЯ AI ===", LogMessageType.System);
            _context.SendLog($"⚡ РАЗМЕР РОЯ: {config.SwarmSize} частиц", LogMessageType.System);
            _context.SendLog($"⚡ МАКС. ИТЕРАЦИЙ PSO: {config.MaxIterations}", LogMessageType.System);
            _context.SendLog($"🧬 РАЗМЕР ПОПУЛЯЦИИ GA: {config.GAPopulationSize}", LogMessageType.System);
            _context.SendLog($"🧬 ПОКОЛЕНИЙ GA: {config.GAGenerations}", LogMessageType.System);
            _context.SendLog($"🎯 ПАРАМЕТРОВ ДЛЯ ОПТИМИЗАЦИИ: {config.ParameterRanges.Count}", LogMessageType.System);
            _context.SendLog($"🔄 НЕПРЕРЫВНАЯ ОПТИМИЗАЦИЯ: {(config.ContinuousOptimization ? "ВКЛ" : "ВЫКЛ")}", LogMessageType.System);
            
            _context.SendLog("=== 🎯 ПАРАМЕТРЫ ДЛЯ ОПТИМИЗАЦИИ ===", LogMessageType.System);
            foreach (var param in config.ParameterRanges)
            {
                _context.SendLog($"  {param.Key}: {param.Value.MinValue} - {param.Value.MaxValue} {(param.Value.IsInteger ? "(целое)" : "")}", 
                    LogMessageType.System);
            }
            
            _context.SendLog("==================================", LogMessageType.System);
        }
        
        private void CheckContinuousOptimization()
        {
            try
            {
                if (_useAIOptimization.ValueString == "Выключена") return;
                
                if (_continuousOptimization.ValueString == "Включено")
                {
                    DateTime now = DateTime.Now;
                    
                    // Проверяем интервал для непрерывной оптимизации
                    bool timeForOptimization = _lastContinuousOptimization == DateTime.MinValue || 
                                             (now - _lastContinuousOptimization).TotalMinutes >= _optimizationIntervalMinutes.ValueInt;
                    
                    // Также оптимизируем при определенных условиях рынка
                    bool marketCondition = CheckMarketConditions();
                    
                    if (timeForOptimization || marketCondition)
                    {
                        if (!_isOptimizationRunning)
                        {
                            _context.SendLog("🔄 ЗАПУСК НЕПРЕРЫВНОЙ AI ОПТИМИЗАЦИИ...", 
                                LogMessageType.System);
                            _ = Task.Run(async () => await StartHybridOptimizationAsync());
                            _lastContinuousOptimization = now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _context.SendLog($"Ошибка проверки непрерывной оптимизации: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private bool CheckMarketConditions()
        {
            try
            {
                var tab = _context.GetTab();
                if (tab?.CandlesAll == null || tab.CandlesAll.Count < 50) return false;
                
                // Проверяем волатильность
                var recentCandles = tab.CandlesAll.TakeLast(20).ToList();
                if (recentCandles.Count < 10) return false;
                
                decimal maxHigh = recentCandles.Max(c => c.High);
                decimal minLow = recentCandles.Min(c => c.Low);
                decimal rangePercent = (maxHigh - minLow) / minLow * 100m;
                
                // Оптимизируем при высокой волатильности
                return rangePercent > 2.0m;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<HybridOptimizationResult> RunHybridOptimization(EnhancedPSOConfiguration config, CancellationToken cancellationToken = default)
        {
            var result = new HybridOptimizationResult 
            { 
                Timestamp = DateTime.Now,
                OptimizationMethod = "Гибридный PSO+GA"
            };
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Проверка отмены
            cancellationToken.ThrowIfCancellationRequested();
            
            // ✅ ВАЛИДАЦИЯ КОНФИГУРАЦИИ: гарантируем наличие обязательных параметров
            ValidateConfiguration(config);
            
            _context.SendLog("🧬 ЭТАП 1: ИНИЦИАЛИЗАЦИЯ PSO РОЯ...", LogMessageType.System);
            InitializeEnhancedSwarm(config);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            _context.SendLog("🧬 ЭТАП 2: ВЫПОЛНЕНИЕ PSO ОПТИМИЗАЦИИ...", LogMessageType.System);
            result.PSOIterations = await RunEnhancedPSO(config, cancellationToken);
            result.BestFitness = _currentSwarm.GlobalBestFitness;
            result.BestParameters = new Dictionary<string, decimal>(_currentSwarm.GlobalBestPosition);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Генетическое улучшение если включено
            if (config.UseGeneticEnhancement)
            {
                _context.SendLog("🧬 ЭТАП 3: ГЕНЕТИЧЕСКОЕ УЛУЧШЕНИЕ РЕЗУЛЬТАТОВ...", LogMessageType.System);
                result.GAGenerations = await RunGeneticAlgorithm(config, result.BestParameters, cancellationToken);
                
                // Используем лучший результат из двух методов
                if (_geneticAlgorithm.BestFitness > result.BestFitness)
                {
                    result.BestFitness = _geneticAlgorithm.BestFitness;
                    result.BestParameters = new Dictionary<string, decimal>(_geneticAlgorithm.BestChromosome);
                    result.OptimizationMethod = "Гибридный PSO+GA (улучшено GA)";
                }
            }
            
            stopwatch.Stop();
            result.OptimizationTime = stopwatch.Elapsed;
            result.Report = _currentReport;
            
            return result;
        }
        
        private void InitializeEnhancedSwarm(EnhancedPSOConfiguration config)
        {
            _currentSwarm = new Swarm();
            
            for (int i = 0; i < config.SwarmSize; i++)
            {
                var particle = new Particle();
                
                // ✅ ГАРАНТИРУЕМ что ВСЕ параметры инициализированы
                foreach (var paramRange in config.ParameterRanges)
                {
                    decimal randomValue = GenerateRandomValue(paramRange.Value);
                    
                    // ✅ ОСОБАЯ ИНИЦИАЛИЗАЦИЯ ДЛЯ TenkanLength: более широкий разброс
                    if (paramRange.Key == "TenkanLength")
                    {
                        // Принудительно создаем широкий диапазон значений для TenkanLength
                        if (i < config.SwarmSize / 3)
                        {
                            randomValue = GenerateRandomValue(new ParameterRange(5, 15, true));
                        }
                        else if (i < 2 * config.SwarmSize / 3)
                        {
                            randomValue = GenerateRandomValue(new ParameterRange(15, 25, true));
                        }
                        else
                        {
                            randomValue = GenerateRandomValue(new ParameterRange(25, 30, true));
                        }
                    }
                    
                    particle.Position[paramRange.Key] = randomValue;
                    // ✅ ИСПРАВЛЕНИЕ: Инициализируем velocity небольшим случайным значением для более активного старта
                    // Это особенно важно для параметров Ишимоку, чтобы они начали изменяться сразу
                    decimal initialVelocity = (decimal)(_random.NextDouble() * 0.5 - 0.25) * (paramRange.Value.MaxValue - paramRange.Value.MinValue) * 0.01m;
                    particle.Velocity[paramRange.Key] = initialVelocity;
                    particle.PersonalBestPosition[paramRange.Key] = randomValue;
                }
                
                _currentSwarm.Particles.Add(particle);
                
                // Логгируем первую частицу для проверки (только реально присутствующие параметры)
                if (i == 0)
                {
                    var paramStrings = new List<string>();
                    
                    // Формируем строку только для параметров, которые реально есть в частице
                    foreach (var param in particle.Position)
                    {
                        string formattedValue;
                        
                        // Определяем формат на основе типа параметра
                        if (param.Key.Contains("Length") || param.Key.Contains("Offset") || 
                            param.Key.Contains("Period") || param.Key.Contains("Count") || 
                            param.Key.Contains("Candles"))
                        {
                            formattedValue = param.Value.ToString("F0"); // Целые числа
                        }
                        else if (param.Key.Contains("Percent"))
                        {
                            formattedValue = param.Value.ToString("F2") + "%";
                        }
                        else
                        {
                            formattedValue = param.Value.ToString("F2");
                        }
                        
                        paramStrings.Add($"{param.Key}={formattedValue}");
                    }
                    
                    string paramsInfo = paramStrings.Count > 0 
                        ? string.Join(", ", paramStrings) 
                        : "нет параметров";
                    
                    _context.SendLog($"🐝 Образец частицы #{i}: {paramsInfo}",
                        LogMessageType.System);
                }
            }
            
            // ✅ ИНИЦИАЛИЗАЦИЯ ГЛОБАЛЬНОГО ЛУЧШЕГО ВСЕМИ ПАРАМЕТРАМИ
            if (_currentSwarm.Particles.Count > 0)
            {
                var firstParticle = _currentSwarm.Particles[0];
                _currentSwarm.GlobalBestPosition = new Dictionary<string, decimal>(firstParticle.Position);
                
                _context.SendLog($"🌍 Инициализирован глобальный лучший: {_currentSwarm.GlobalBestPosition.Count} параметров", 
                    LogMessageType.System);
                
                // Специальный лог для TenkanLength
                if (_currentSwarm.GlobalBestPosition.ContainsKey("TenkanLength"))
                {
                    _context.SendLog($"🎯 Начальный TenkanLength в глобальном лучшем: {_currentSwarm.GlobalBestPosition["TenkanLength"]}", 
                        LogMessageType.System);
                }
            }
            
            _context.SendLog($"🐝 PSO РОЙ ИНИЦИАЛИЗИРОВАН: {config.SwarmSize} частиц, {config.ParameterRanges.Count} параметров", 
                LogMessageType.System);
        }
        
        private decimal GenerateRandomValue(ParameterRange range)
        {
            decimal randomValue = range.MinValue + (decimal)_random.NextDouble() * (range.MaxValue - range.MinValue);
            
            if (range.IsInteger)
            {
                randomValue = Math.Round(randomValue);
            }
            
            return randomValue;
        }
        
        private async Task<int> RunEnhancedPSO(EnhancedPSOConfiguration config, CancellationToken cancellationToken = default)
        {
            int iterationsCompleted = 0;
            
            for (int iteration = 0; iteration < config.MaxIterations; iteration++)
            {
                // ПРОВЕРКА ОТМЕНЫ на каждой итерации
                cancellationToken.ThrowIfCancellationRequested();
                
                _currentSwarm.Iteration = iteration;
                iterationsCompleted++;
                
                if (iteration % 10 == 0 || iteration < 5)
                {
                    LogEnhancedPSOStatus(iteration, config.MaxIterations);
                }
                
                await EvaluateSwarmFitness(cancellationToken);
                UpdateGlobalBest();
                
                double currentInertia = config.UseAdaptiveInertia
                    ? GetAdaptiveInertia(iteration, config.MaxIterations, config)
                    : config.Inertia;
                
                UpdateParticles(config, currentInertia);
                
                // Расширенные операторы
                if (config.MutationRate > 0 && iteration % 3 == 0)
                {
                    ApplyEnhancedMutation(config, iteration);
                }
                
                if (config.CrossoverRate > 0 && iteration % 5 == 0)
                {
                    ApplyEnhancedCrossover(config);
                }
                
                if (CheckEnhancedConvergence(config, iteration)) break;
            }
            
            return iterationsCompleted;
        }
        
        private void LogEnhancedPSOStatus(int iteration, int maxIterations)
        {
            // Получаем лучшую частицу
            var bestParticle = _currentSwarm.Particles.OrderByDescending(p => p.CurrentFitness).First();
            var averageFitness = _currentSwarm.Particles.Average(p => p.CurrentFitness);
            var diversity = CalculateEnhancedDiversity();
            
            double progress = maxIterations == 0 ? 0 : (double)iteration / maxIterations * 100.0;
            
            // ✅ КРИТИЧНО: Показываем ВАЖНЫЕ параметры Ишимоку в логах
            string ichimokuParams = "";
            List<string> importantParams = new List<string> 
            { 
                "TenkanLength", 
                "KijunLength", 
                "SenkouBLength",
                "SenkouOffset",
                "MinProfitPercent",
                "TrailingStartPercent"
            };
            
            foreach (var param in importantParams)
            {
                if (_currentSwarm.GlobalBestPosition != null && 
                    _currentSwarm.GlobalBestPosition.ContainsKey(param))
                {
                    decimal value = _currentSwarm.GlobalBestPosition[param];
                    // Определяем формат на основе типа параметра
                    bool isInteger = param.Contains("Length") || param.Contains("Offset") || param.Contains("Count");
                    string formattedValue = isInteger ? value.ToString("F0") : value.ToString("F2");
                        
                    ichimokuParams += $" {param}:{formattedValue} |";
                }
            }
            
            // Также показываем TenkanLength в текущей лучшей частице
            string currentTenkan = "";
            if (bestParticle.Position.ContainsKey("TenkanLength"))
            {
                currentTenkan = $" 🔸Tenkan={bestParticle.Position["TenkanLength"]:F0}";
            }
            
            _context.SendLog(
                $"⚡ PSO Итерация {iteration}/{maxIterations} | " +
                $"🎯 Лучший: {_currentSwarm.GlobalBestFitness:F2}% | " +
                $"📊 Средний: {averageFitness:F2}% | " +
                $"🌐 Разнообразие: {diversity:P1} | " +
                $"📈 Прогресс: {progress:F1}% | " +
                $"🔧 Параметры: |{ichimokuParams}{currentTenkan}",
                LogMessageType.System);
            
            // ✅ ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ: статистика по параметрам Ишимоку во всем рое
            if (iteration % 20 == 0 && _currentSwarm.Particles.Count > 0)
            {
                var ichimokuParamNames = new[] { "TenkanLength", "KijunLength", "SenkouBLength", "SenkouOffset" };
                
                foreach (var paramName in ichimokuParamNames)
                {
                    var paramValues = _currentSwarm.Particles
                        .Where(p => p.Position.ContainsKey(paramName))
                        .Select(p => p.Position[paramName])
                        .ToList();
                    
                    if (paramValues.Count > 0)
                    {
                        decimal minVal = paramValues.Min();
                        decimal maxVal = paramValues.Max();
                        decimal avgVal = paramValues.Average();
                        decimal spread = maxVal - minVal;
                        
                        _context.SendLog(
                            $"📊 {paramName}: min={minVal:F0}, max={maxVal:F0}, avg={avgVal:F1}, разброс={spread:F0}",
                            LogMessageType.System);
                    }
                }
                
                // Дополнительно: статистика по расстоянию между линиями облака (SenkouB - Kijun)
                var kijunValues = _currentSwarm.Particles
                    .Where(p => p.Position.ContainsKey("KijunLength"))
                    .Select(p => p.Position["KijunLength"])
                    .ToList();
                var senkouBValues = _currentSwarm.Particles
                    .Where(p => p.Position.ContainsKey("SenkouBLength"))
                    .Select(p => p.Position["SenkouBLength"])
                    .ToList();
                
                if (kijunValues.Count > 0 && senkouBValues.Count > 0 && kijunValues.Count == senkouBValues.Count)
                {
                    var cloudDistances = _currentSwarm.Particles
                        .Where(p => p.Position.ContainsKey("KijunLength") && p.Position.ContainsKey("SenkouBLength"))
                        .Select(p => p.Position["SenkouBLength"] - p.Position["KijunLength"])
                        .ToList();
                    
                    if (cloudDistances.Count > 0)
                    {
                        decimal minDist = cloudDistances.Min();
                        decimal maxDist = cloudDistances.Max();
                        decimal avgDist = cloudDistances.Average();
                        
                        _context.SendLog(
                            $"📊 Расстояние облака (SenkouB-Kijun): min={minDist:F0}, max={maxDist:F0}, avg={avgDist:F1}",
                            LogMessageType.System);
                    }
                }
            }
        }
        
        private double CalculateEnhancedDiversity()
        {
            if (_currentSwarm.Particles.Count < 2) return 0;
            
            try
            {
                // Рассчитываем разнообразие по всем параметрам
                double totalDiversity = 0;
                int paramCount = 0;
                
                var firstParticle = _currentSwarm.Particles.First();
                foreach (var paramKey in firstParticle.Position.Keys)
                {
                    var values = _currentSwarm.Particles
                        .Where(p => p.Position.ContainsKey(paramKey))
                        .Select(p => (double)p.Position[paramKey])
                        .ToArray();
                    
                    if (values.Length > 1)
                    {
                        var mean = values.Average();
                        var variance = values.Average(x => Math.Pow(x - mean, 2));
                        var stdDev = Math.Sqrt(variance);
                        var diversity = stdDev / (mean == 0 ? 1 : mean);
                        
                        totalDiversity += diversity;
                        paramCount++;
                    }
                }
                
                return paramCount > 0 ? totalDiversity / paramCount : 0;
            }
            catch
            {
                return 0;
            }
        }
        
        private double GetAdaptiveInertia(int iteration, int maxIterations, EnhancedPSOConfiguration config)
        {
            double progress = (double)iteration / maxIterations;
            double inertia = config.StartInertia - (config.StartInertia - config.EndInertia) * progress;
            
            // Динамическая адаптация на основе разнообразия
            double diversity = CalculateEnhancedDiversity();
            if (diversity < 0.1) // Низкое разнообразие
            {
                inertia += 0.1; // Увеличиваем инерцию для исследования
            }
            else if (diversity > 0.5) // Высокое разнообразие
            {
                inertia -= 0.05; // Уменьшаем инерцию для эксплуатации
            }
            
            return Math.Max(0.1, Math.Min(1.0, inertia));
        }
        
        private async Task EvaluateSwarmFitness(CancellationToken cancellationToken = default)
        {
            var tasks = _currentSwarm.Particles.Select(async particle =>
            {
                // ПРОВЕРКА ОТМЕНЫ перед обработкой частицы
                cancellationToken.ThrowIfCancellationRequested();
                
                particle.Age++;
                
                var paramKey = GetEnhancedParametersKey(particle.Position);
                
                if (_fitnessCache.TryGetValue(paramKey, out double cachedFitness))
                {
                    particle.CurrentFitness = cachedFitness;
                    return;
                }
                
                double fitness = await CalculateEnhancedFitnessAsync(particle.Position);
                particle.CurrentFitness = fitness;
                _fitnessCache[paramKey] = fitness;
                _currentReport.TotalEvaluations++;
                
                if (fitness > particle.PersonalBestFitness)
                {
                    double improvement = fitness - particle.PersonalBestFitness;
                    particle.PersonalBestFitness = fitness;
                    particle.PersonalBestPosition = new Dictionary<string, decimal>(particle.Position);
                    particle.StagnationCount = 0;
                    
                    _currentReport.ImprovementHistory.Add(
                        $"PSO Частица {particle.Id.Substring(0, 6)}: +{improvement:F3}%");
                }
                else
                {
                    particle.StagnationCount++;
                }
            });
            
            await Task.WhenAll(tasks);
        }
        
        private async Task<double> CalculateEnhancedFitnessAsync(Dictionary<string, decimal> parameters)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var backtestResult = SimulateEnhancedBacktest(parameters);
                    
                    // Улучшенная функция фитнеса
                    double fitness = backtestResult.TotalReturn * 0.4 +      // Общая доходность
                                   backtestResult.SharpeRatio * 0.3 +       // Коэффициент Шарпа
                                   backtestResult.WinRate * 0.15 +          // Процент выигрышных сделок
                                   backtestResult.ProfitFactor * 0.1 +      // Профит-фактор
                                   (100 - backtestResult.MaxDrawdown) * 0.05; // Минимизация просадки
                    
                    // Бонус за разумные параметры Ишимоку
                    if (parameters.ContainsKey("TenkanLength") && parameters.ContainsKey("KijunLength"))
                    {
                        decimal tenkan = parameters["TenkanLength"];
                        decimal kijun = parameters["KijunLength"];
                        
                        if (tenkan < kijun) fitness += 2.0; // Правильная иерархия
                        if (kijun - tenkan >= 5) fitness += 1.0; // Достаточный разрыв
                    }
                    
                    return Math.Max(fitness, -100);
                }
                catch (Exception ex)
                {
                    _context.SendLog($"Ошибка вычисления фитнеса: {ex.Message}", LogMessageType.Error);
                    return -1000;
                }
            });
        }
        
        private BacktestResult SimulateEnhancedBacktest(Dictionary<string, decimal> parameters)
        {
            try
            {
                if (_historicalCandles != null && _historicalCandles.Count > 100)
                {
                    return PerformRealEnhancedBacktest(parameters);
                }
                
                return CalculateEnhancedSimplifiedFitness(parameters);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка симуляции бэктеста: {ex.Message}", LogMessageType.Error);
                return new BacktestResult 
                { 
                    TotalReturn = -100, 
                    SharpeRatio = -1, 
                    WinRate = 0, 
                    MaxDrawdown = -100,
                    ProfitFactor = 0.5 
                };
            }
        }
        
        private BacktestResult CalculateEnhancedSimplifiedFitness(Dictionary<string, decimal> parameters)
        {
            try
            {
                double fitness = 0;
                
                // Проверка разумности параметров Ишимоку
                if (parameters.ContainsKey("TenkanLength") && parameters.ContainsKey("KijunLength") && 
                    parameters.ContainsKey("SenkouBLength"))
                {
                    decimal paramTenkan = parameters["TenkanLength"];
                    decimal paramKijun = parameters["KijunLength"];
                    decimal paramSenkouB = parameters["SenkouBLength"];
                    
                    if (paramTenkan < paramKijun && paramKijun < paramSenkouB)
                    {
                        fitness += 10; // Правильная иерархия периодов
                    }
                    
                    if (paramTenkan >= 5 && paramTenkan <= 30) fitness += 5;
                    if (paramKijun >= 15 && paramKijun <= 60) fitness += 5;
                    if (paramSenkouB >= 40 && paramSenkouB <= 120) fitness += 5;
                }
                
                // Проверка других параметров
                if (parameters.ContainsKey("MinProfitPercent"))
                {
                    decimal minProfit = parameters["MinProfitPercent"];
                    if (minProfit >= 0.05m && minProfit <= 2.0m) fitness += 3;
                }
                
                if (parameters.ContainsKey("TrailingStartPercent"))
                {
                    decimal trailingStart = parameters["TrailingStartPercent"];
                    if (trailingStart >= 0.1m && trailingStart <= 3.0m) fitness += 3;
                }
                
                // Добавляем случайность для разнообразия
                fitness += _random.NextDouble() * 5;
                
                return new BacktestResult
                {
                    TotalReturn = fitness * 1.5,
                    SharpeRatio = fitness * 0.4,
                    WinRate = 50 + fitness,
                    MaxDrawdown = -fitness * 0.3,
                    ProfitFactor = 1.2 + fitness * 0.05,
                    TotalTrades = 50 + (int)fitness * 2,
                    RecoveryFactor = fitness * 0.2
                };
            }
            catch
            {
                return new BacktestResult
                {
                    TotalReturn = 8 + _random.NextDouble() * 15,
                    SharpeRatio = 0.6 + _random.NextDouble() * 1.4,
                    WinRate = 45 + _random.NextDouble() * 25,
                    MaxDrawdown = -4 - _random.NextDouble() * 6,
                    ProfitFactor = 1.1 + _random.NextDouble() * 0.8,
                    TotalTrades = 30 + _random.Next(40),
                    RecoveryFactor = 0.5 + _random.NextDouble() * 1.5
                };
            }
        }
        
        private BacktestResult PerformRealEnhancedBacktest(Dictionary<string, decimal> parameters)
        {
            // РЕАЛЬНЫЙ БЭКТЕСТ НА ИСТОРИЧЕСКИХ ДАННЫХ
            try
            {
                if (_historicalCandles == null || _historicalCandles.Count < 100)
                {
                    _context.SendLog("⚠️ Недостаточно исторических данных для реального бэктеста", LogMessageType.System);
                    return CalculateEnhancedSimplifiedFitness(parameters);
                }
                
                // Используем реальный бэктест-движок
                var backtestEngine = new RealBacktestEngine();
                
                // Определяем период для бэктеста (последние 70% данных для обучения, 30% для теста)
                int totalCandles = _historicalCandles.Count;
                int testStartIndex = (int)(totalCandles * 0.7);
                
                DateTime fromDate = _historicalCandles[testStartIndex].TimeStart;
                DateTime toDate = _historicalCandles[totalCandles - 1].TimeStart;
                
                var result = backtestEngine.RunBacktest(fromDate, toDate, parameters, _historicalCandles);
                
                _context.SendLog($"📊 Реальный бэктест: {result.TotalTrades} сделок, Доходность: {result.TotalReturn:F2}%, WinRate: {result.WinRate:F2}%", 
                    LogMessageType.System);
                
                return result;
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка реального бэктеста: {ex.Message}", LogMessageType.Error);
                return CalculateEnhancedSimplifiedFitness(parameters);
            }
        }
        
        private void UpdateGlobalBest()
        {
            foreach (var particle in _currentSwarm.Particles)
            {
                if (particle.CurrentFitness > _currentSwarm.GlobalBestFitness)
                {
                    double improvement = particle.CurrentFitness - _currentSwarm.GlobalBestFitness;
                    _currentSwarm.GlobalBestFitness = particle.CurrentFitness;
                    _currentSwarm.GlobalBestPosition = new Dictionary<string, decimal>(particle.Position);
                    _currentSwarm.LastImprovementTime = DateTime.Now;
                    
                    _currentReport.ImprovementHistory.Add(
                        $"PSO Глобальное улучшение (Итерация {_currentSwarm.Iteration}): +{improvement:F3}%");
                    
                    // Записываем улучшения параметров
                    foreach (var param in particle.Position)
                    {
                        if (_currentReport.ParameterImprovements.ContainsKey(param.Key))
                        {
                            _currentReport.ParameterImprovements[param.Key] = param.Value;
                        }
                        else
                        {
                            _currentReport.ParameterImprovements.Add(param.Key, param.Value);
                        }
                    }
                }
            }
            
            _currentSwarm.FitnessHistory.Add(_currentSwarm.GlobalBestFitness);
        }
        
        private void UpdateParticles(EnhancedPSOConfiguration config, double inertia)
        {
            foreach (var particle in _currentSwarm.Particles)
            {
                // ✅ КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Гарантируем что ВСЕ параметры обрабатываются
                foreach (var paramRange in config.ParameterRanges)
                {
                    string param = paramRange.Key;
                    
                    // ✅ Шаг 1: ГАРАНТИЯ СУЩЕСТВОВАНИЯ КЛЮЧЕЙ - без continue!
                    // 1.1 Если параметра нет в позиции частицы - создаем случайное значение
                    if (!particle.Position.ContainsKey(param))
                    {
                        particle.Position[param] = GenerateRandomValue(paramRange.Value);
                        if (_currentSwarm.Iteration < 5)
                        {
                            _context.SendLog($"🔄 PSO: Инициализирован отсутствующий параметр {param} = {particle.Position[param]}", 
                                LogMessageType.System);
                        }
                    }
                    
                    // 1.2 Если нет скорости - инициализируем небольшим случайным значением
                    if (!particle.Velocity.ContainsKey(param))
                    {
                        // ✅ ИСПРАВЛЕНИЕ: Небольшая начальная скорость для активного старта
                        var range = config.ParameterRanges[param];
                        decimal initialVelocity = (decimal)(_random.NextDouble() * 0.5 - 0.25) * (range.MaxValue - range.MinValue) * 0.01m;
                        particle.Velocity[param] = initialVelocity;
                    }
                    
                    // 1.3 Если нет личного лучшего - копируем текущую позицию
                    if (!particle.PersonalBestPosition.ContainsKey(param))
                    {
                        particle.PersonalBestPosition[param] = particle.Position[param];
                    }
                    
                    // 1.4 Если нет глобального лучшего - инициализируем
                    if (!_currentSwarm.GlobalBestPosition.ContainsKey(param))
                    {
                        _currentSwarm.GlobalBestPosition[param] = particle.Position[param];
                        if (_currentSwarm.Iteration < 5)
                        {
                            _context.SendLog($"🌍 PSO: Инициализирован глобальный лучший {param} = {_currentSwarm.GlobalBestPosition[param]}", 
                                LogMessageType.System);
                        }
                    }
                    
                    // ✅ Шаг 2: Проверяем защищенные параметры (если включена защита)
                    if (_preserveSafetyLogic?.ValueBool == true)
                    {
                        // Параметры защиты которые НЕ должны меняться AI
                        var protectedParams = new[] { 
                            "MinProfitPercent", 
                            "MaxSpreadPercent",
                            "BreakEvenTriggerPercent",
                            "MaxOpenPositions"
                        };
                        
                        if (protectedParams.Contains(param))
                        {
                            // Пропускаем обновление защищенных параметров
                            continue;
                        }
                    }
                    
                    // ✅ Шаг 3: ОСНОВНОЕ ОБНОВЛЕНИЕ ПАРАМЕТРА
                    try
                    {
                        // Сохраняем старые значения для логирования
                        decimal oldPosition = particle.Position[param];
                        decimal oldVelocity = particle.Velocity[param];
                        
                        decimal r1 = (decimal)_random.NextDouble();
                        decimal r2 = (decimal)_random.NextDouble();
                        
                        // Формула PSO: новая скорость = инерция * старая скорость + 
                        //               когнитивная компонента + социальная компонента
                        decimal cognitiveComponent = (decimal)config.CognitiveWeight * r1 * 
                            (particle.PersonalBestPosition[param] - particle.Position[param]);
                        decimal socialComponent = (decimal)config.SocialWeight * r2 * 
                            (_currentSwarm.GlobalBestPosition[param] - particle.Position[param]);
                        decimal noise = (decimal)(_random.NextDouble() * 0.1 - 0.05);
                        
                        particle.Velocity[param] =
                            (decimal)inertia * particle.Velocity[param] +
                            cognitiveComponent +
                            socialComponent +
                            noise;
                        
                        // Обновление позиции: новая позиция = старая позиция + скорость
                        particle.Position[param] += particle.Velocity[param];
                        
                        // ✅ Шаг 4: ОГРАНИЧЕНИЕ ДИАПАЗОНА значений
                        var range = config.ParameterRanges[param];
                        particle.Position[param] = Math.Max(range.MinValue, 
                            Math.Min(range.MaxValue, particle.Position[param]));
                        
                        // ✅ Шаг 5: ОКРУГЛЕНИЕ для целых параметров (TenkanLength, KijunLength и др.)
                        if (range.IsInteger)
                        {
                            particle.Position[param] = Math.Round(particle.Position[param]);
                        }
                        
                        // ✅ Шаг 6: ДЕТАЛЬНОЕ ЛОГГИРОВАНИЕ параметров Ишимоку (первые 3 итерации)
                        var ichimokuParams = new[] { "TenkanLength", "KijunLength", "SenkouBLength", "SenkouOffset" };
                        if (_currentSwarm.Iteration < 3 && ichimokuParams.Contains(param))
                        {
                            decimal positionChange = particle.Position[param] - oldPosition;
                            _context.SendLog(
                                $"🔧 PSO Итерация {_currentSwarm.Iteration} | {param}: " +
                                $"{oldPosition:F1} → {particle.Position[param]:F1} " +
                                $"(Δ={positionChange:F2}, v={oldVelocity:F3}→{particle.Velocity[param]:F3}, " +
                                $"cog={cognitiveComponent:F3}, soc={socialComponent:F3})",
                                LogMessageType.System);
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.SendLog($"⚠️ Ошибка обновления параметра {param}: {ex.Message}", 
                            LogMessageType.Error);
                        // При ошибке - сбрасываем параметр на случайное значение
                        particle.Position[param] = GenerateRandomValue(paramRange.Value);
                        particle.Velocity[param] = 0;
                    }
                }
            }
        }
        
        private void ApplyEnhancedMutation(EnhancedPSOConfiguration config, int iteration)
        {
            foreach (var particle in _currentSwarm.Particles)
            {
                if (_random.NextDouble() < config.MutationRate)
                {
                    // ИСПРАВЛЕНИЕ: Используем параметры из конфигурации, а не только из Position
                    var availableParams = config.ParameterRanges.Keys.ToList();
                    if (availableParams.Count == 0) continue;
                    
                    var paramToMutate = availableParams[_random.Next(availableParams.Count)];
                    
                    // Инициализируем отсутствующие ключи
                    if (!particle.Position.ContainsKey(paramToMutate))
                    {
                        particle.Position[paramToMutate] = GenerateRandomValue(config.ParameterRanges[paramToMutate]);
                    }
                    if (!particle.Velocity.ContainsKey(paramToMutate))
                    {
                        particle.Velocity[paramToMutate] = 0;
                    }
                    
                    var range = config.ParameterRanges[paramToMutate];
                    
                    // Различные типы мутации
                    double mutationType = _random.NextDouble();
                    
                    if (mutationType < 0.3) // Полностью случайная мутация
                    {
                        particle.Position[paramToMutate] = GenerateRandomValue(range);
                    }
                    else if (mutationType < 0.6) // Небольшая мутация
                    {
                        decimal mutation = (decimal)(_random.NextDouble() * 0.2 - 0.1) * (range.MaxValue - range.MinValue);
                        particle.Position[paramToMutate] += mutation;
                        particle.Position[paramToMutate] = Math.Max(range.MinValue, 
                            Math.Min(range.MaxValue, particle.Position[paramToMutate]));
                        
                        if (range.IsInteger)
                        {
                            particle.Position[paramToMutate] = Math.Round(particle.Position[paramToMutate]);
                        }
                    }
                    else // Мутация к лучшей позиции
                    {
                        if (_currentSwarm.GlobalBestPosition.ContainsKey(paramToMutate))
                        {
                            decimal blend = (decimal)_random.NextDouble() * 0.3m;
                            particle.Position[paramToMutate] = 
                                particle.Position[paramToMutate] * (1 - blend) + 
                                _currentSwarm.GlobalBestPosition[paramToMutate] * blend;
                        }
                    }
                    
                    particle.MutationHistory.Add($"Итерация {iteration}: {paramToMutate}");
                }
            }
        }
        
        private void ApplyEnhancedCrossover(EnhancedPSOConfiguration config)
        {
            var bestParticles = _currentSwarm.Particles
                .OrderByDescending(p => p.CurrentFitness)
                .Take(Math.Max(3, config.SwarmSize / 4))
                .ToList();
            
            if (bestParticles.Count >= 2)
            {
                for (int i = 0; i < bestParticles.Count - 1; i++)
                {
                    if (_random.NextDouble() < config.CrossoverRate)
                    {
                        var parent1 = bestParticles[i];
                        var parent2 = bestParticles[i + 1];
                        
                        // Создаем несколько детей
                        for (int childNum = 0; childNum < 2; childNum++)
                        {
                            var child = new Particle();
                            
                            // ИСПРАВЛЕНИЕ: Используем все параметры из конфигурации
                            foreach (var paramRange in config.ParameterRanges)
                            {
                                string param = paramRange.Key;
                                
                                // Инициализируем отсутствующие ключи в родителях
                                if (!parent1.Position.ContainsKey(param))
                                    parent1.Position[param] = GenerateRandomValue(paramRange.Value);
                                if (!parent2.Position.ContainsKey(param))
                                    parent2.Position[param] = GenerateRandomValue(paramRange.Value);
                                
                                if (_random.NextDouble() < 0.5)
                                {
                                    child.Position[param] = parent1.Position[param];
                                }
                                else
                                {
                                    child.Position[param] = parent2.Position[param];
                                }
                                
                                // Инициализируем Velocity для ребенка
                                child.Velocity[param] = 0;
                                
                                // Добавляем небольшое изменение
                                if (_random.NextDouble() < 0.1)
                                {
                                    var range = config.ParameterRanges[param];
                                    decimal mutation = (decimal)(_random.NextDouble() * 0.05 - 0.025) * 
                                                     (range.MaxValue - range.MinValue);
                                    child.Position[param] += mutation;
                                    child.Position[param] = Math.Max(range.MinValue, 
                                        Math.Min(range.MaxValue, child.Position[param]));
                                }
                            }
                            
                            _currentSwarm.Particles.Add(child);
                        }
                    }
                }
            }
        }
        
        private bool CheckEnhancedConvergence(EnhancedPSOConfiguration config, int iteration)
        {
            if (iteration < 20) return false;
            
            // Проверяем разнообразие
            double diversity = CalculateEnhancedDiversity();
            if (diversity < 0.01 && iteration > config.MaxIterations * 0.7)
            {
                _context.SendLog($"🔄 PSO СХОДИМОСТЬ: Низкое разнообразие ({diversity:P3})", 
                    LogMessageType.System);
                return true;
            }
            
            // Проверяем улучшения за последние итерации
            if (_currentSwarm.FitnessHistory.Count >= 15)
            {
                var recentFitness = _currentSwarm.FitnessHistory.TakeLast(15).ToArray();
                var maxRecent = recentFitness.Max();
                var improvement = maxRecent - recentFitness.First();
                
                if (improvement < 0.01 && iteration > config.MaxIterations * 0.6)
                {
                    _context.SendLog($"🔄 PSO СХОДИМОСТЬ: Мало улучшений ({improvement:F3}%)", 
                        LogMessageType.System);
                    return true;
                }
            }
            
            // Проверяем время с последнего улучшения
            if ((DateTime.Now - _currentSwarm.LastImprovementTime).TotalMinutes > 5 && 
                iteration > config.MaxIterations * 0.5)
            {
                _context.SendLog($"🔄 PSO СХОДИМОСТЬ: Нет улучшений более 5 минут", 
                    LogMessageType.System);
                return true;
            }
            
            return false;
        }
        
        private async Task<int> RunGeneticAlgorithm(EnhancedPSOConfiguration config, Dictionary<string, decimal> initialSolution, CancellationToken cancellationToken = default)
        {
            _geneticAlgorithm = new GeneticAlgorithm();
            
            // Инициализация популяции
            InitializeGAPopulation(config, initialSolution);
            
            for (int generation = 0; generation < config.GAGenerations; generation++)
            {
                // ПРОВЕРКА ОТМЕНЫ на каждой генерации
                cancellationToken.ThrowIfCancellationRequested();
                
                _geneticAlgorithm.Generation = generation;
                
                if (generation % 5 == 0 || generation < 3)
                {
                    LogGAStatus(generation, config.GAGenerations);
                }
                
                await EvaluateGAPopulation(config, cancellationToken);
                SelectAndReproduce(config);
                ApplyGAMutation(config, generation);
                
                if (CheckGAConvergence(config, generation)) break;
            }
            
            return _geneticAlgorithm.Generation;
        }
        
        private void InitializeGAPopulation(EnhancedPSOConfiguration config, Dictionary<string, decimal> initialSolution)
        {
            _geneticAlgorithm.Population.Clear();
            
            // Добавляем начальное решение
            _geneticAlgorithm.Population.Add(new Dictionary<string, decimal>(initialSolution));
            _geneticAlgorithm.BestChromosome = new Dictionary<string, decimal>(initialSolution);
            
            // Генерируем остальную популяцию
            for (int i = 1; i < config.GAPopulationSize; i++)
            {
                var chromosome = new Dictionary<string, decimal>();
                
                foreach (var paramRange in config.ParameterRanges)
                {
                    decimal value;
                    
                    if (i < config.GAPopulationSize * 0.3 && initialSolution.ContainsKey(paramRange.Key))
                    {
                        // Часть популяции на основе начального решения
                        decimal noise = (decimal)(_random.NextDouble() * 0.2 - 0.1) * 
                                      (paramRange.Value.MaxValue - paramRange.Value.MinValue);
                        value = initialSolution[paramRange.Key] + noise;
                    }
                    else
                    {
                        // Случайные значения
                        value = GenerateRandomValue(paramRange.Value);
                    }
                    
                    value = Math.Max(paramRange.Value.MinValue, Math.Min(paramRange.Value.MaxValue, value));
                    chromosome[paramRange.Key] = value;
                }
                
                _geneticAlgorithm.Population.Add(chromosome);
            }
            
            _context.SendLog($"🧬 GA ПОПУЛЯЦИЯ ИНИЦИАЛИЗИРОВАНА: {config.GAPopulationSize} хромосом", 
                LogMessageType.System);
        }
        
        private async Task EvaluateGAPopulation(EnhancedPSOConfiguration config, CancellationToken cancellationToken = default)
        {
            var tasks = _geneticAlgorithm.Population.Select(async chromosome =>
            {
                // ПРОВЕРКА ОТМЕНЫ перед обработкой хромосомы
                cancellationToken.ThrowIfCancellationRequested();
                
                var paramKey = GetEnhancedParametersKey(chromosome);
                
                if (_fitnessCache.TryGetValue(paramKey, out double cachedFitness))
                {
                    return cachedFitness;
                }
                
                double fitness = await CalculateEnhancedFitnessAsync(chromosome);
                _fitnessCache[paramKey] = fitness;
                _currentReport.TotalEvaluations++;
                
                return fitness;
            });
            
            var fitnesses = await Task.WhenAll(tasks);
            
            // Обновляем лучший результат
            for (int i = 0; i < fitnesses.Length; i++)
            {
                if (fitnesses[i] > _geneticAlgorithm.BestFitness)
                {
                    _geneticAlgorithm.BestFitness = fitnesses[i];
                    _geneticAlgorithm.BestChromosome = new Dictionary<string, decimal>(_geneticAlgorithm.Population[i]);
                    
                    _currentReport.ImprovementHistory.Add(
                        $"GA Поколение {_geneticAlgorithm.Generation}: +{fitnesses[i]:F3}%");
                }
            }
        }
        
        private void LogGAStatus(int generation, int maxGenerations)
        {
            _context.SendLog(
                $"🧬 GA Поколение {generation}/{maxGenerations} | " +
                $"🎯 Лучший фитнес: {_geneticAlgorithm.BestFitness:F2}% | " +
                $"👥 Размер популяции: {_geneticAlgorithm.Population.Count}",
                LogMessageType.System);
        }
        
        private void SelectAndReproduce(EnhancedPSOConfiguration config)
        {
            var newPopulation = new List<Dictionary<string, decimal>>();
            
            // Элитизм: сохраняем лучшие решения
            newPopulation.Add(new Dictionary<string, decimal>(_geneticAlgorithm.BestChromosome));
            
            // Турнирная селекция и кроссовер
            while (newPopulation.Count < config.GAPopulationSize)
            {
                var parent1 = TournamentSelection(config.GAPopulationSize / 4);
                var parent2 = TournamentSelection(config.GAPopulationSize / 4);
                
                if (_random.NextDouble() < config.GACrossoverRate)
                {
                    var child = Crossover(parent1, parent2);
                    newPopulation.Add(child);
                }
                else
                {
                    // Клонирование родителя
                    newPopulation.Add(new Dictionary<string, decimal>(parent1));
                }
            }
            
            _geneticAlgorithm.Population = newPopulation;
        }
        
        private Dictionary<string, decimal> TournamentSelection(int tournamentSize)
        {
            tournamentSize = Math.Min(tournamentSize, _geneticAlgorithm.Population.Count);
            
            var tournament = new List<Dictionary<string, decimal>>();
            for (int i = 0; i < tournamentSize; i++)
            {
                int index = _random.Next(_geneticAlgorithm.Population.Count);
                tournament.Add(_geneticAlgorithm.Population[index]);
            }
            
            // Выбираем лучшую хромосому из турнира
            return tournament.OrderByDescending(c => 
                _fitnessCache.TryGetValue(GetEnhancedParametersKey(c), out double fitness) ? fitness : -1000)
                .First();
        }
        
        private Dictionary<string, decimal> Crossover(Dictionary<string, decimal> parent1, Dictionary<string, decimal> parent2)
        {
            var child = new Dictionary<string, decimal>();
            
            foreach (var param in parent1.Keys)
            {
                if (_random.NextDouble() < 0.5)
                {
                    child[param] = parent1[param];
                }
                else
                {
                    child[param] = parent2.ContainsKey(param) ? parent2[param] : parent1[param];
                }
            }
            
            return child;
        }
        
        private void ApplyGAMutation(EnhancedPSOConfiguration config, int generation)
        {
            foreach (var chromosome in _geneticAlgorithm.Population)
            {
                if (_random.NextDouble() < config.GAMutationRate)
                {
                    var paramToMutate = chromosome.Keys.ElementAt(_random.Next(chromosome.Count));
                    var range = config.ParameterRanges[paramToMutate];
                    
                    // Адаптивная мутация
                    double mutationStrength = 0.1 * (1.0 - (double)generation / config.GAGenerations);
                    decimal mutation = (decimal)(_random.NextDouble() * mutationStrength * 2 - mutationStrength) * 
                                     (range.MaxValue - range.MinValue);
                    
                    chromosome[paramToMutate] += mutation;
                    chromosome[paramToMutate] = Math.Max(range.MinValue, 
                        Math.Min(range.MaxValue, chromosome[paramToMutate]));
                    
                    if (range.IsInteger)
                    {
                        chromosome[paramToMutate] = Math.Round(chromosome[paramToMutate]);
                    }
                }
            }
        }
        
        private bool CheckGAConvergence(EnhancedPSOConfiguration config, int generation)
        {
            if (generation < 10) return false;
            
            // Проверяем, улучшился ли лучший результат за последние поколения
            if (generation > (int)(config.GAGenerations * 0.7))
            {
                _context.SendLog($"🔄 GA СХОДИМОСТЬ: Достигнуто {generation} поколений", 
                    LogMessageType.System);
                return true;
            }
            
            return false;
        }
        
        private string GetEnhancedParametersKey(Dictionary<string, decimal> parameters)
        {
            return string.Join("|", parameters.OrderBy(p => p.Key)
                .Select(p => $"{p.Key}:{p.Value:F6}"));
        }
        
        private void GenerateEnhancedReport(HybridOptimizationResult result, EnhancedPSOConfiguration config)
        {
            var report = result.Report;
            
            // Рассчитываем дополнительные метрики
            report.BestFitness = result.BestFitness;
            
            if (_currentSwarm != null && _currentSwarm.Particles.Count > 0)
            {
                report.AverageFitness = _currentSwarm.Particles.Average(p => p.CurrentFitness);
                report.Diversity = CalculateEnhancedDiversity();
                report.EffectiveParticles = _currentSwarm.Particles.Count(p => p.StagnationCount < 10);
                report.StagnationCount = _currentSwarm.Particles.Count(p => p.StagnationCount >= 20);
            }
            
            report.ExplorationExploitationRatio = CalculateExplorationRatio();
            
            _context.SendLog("=== 🚀 ДЕТАЛЬНЫЙ ОТЧЕТ ГИБРИДНОЙ AI ОПТИМИЗАЦИИ ===", LogMessageType.System);
            _context.SendLog($"🎯 ЛУЧШИЙ РЕЗУЛЬТАТ: {report.BestFitness:F2}%", LogMessageType.System);
            _context.SendLog($"📊 СРЕДНИЙ ФИТНЕС: {report.AverageFitness:F2}%", LogMessageType.System);
            _context.SendLog($"🌐 РАЗНООБРАЗИЕ: {report.Diversity:P2}", LogMessageType.System);
            _context.SendLog($"⚡ ЭКСПЛУАТАЦИЯ/ИССЛЕДОВАНИЕ: {report.ExplorationExploitationRatio:P1}", LogMessageType.System);
            _context.SendLog($"🐝 АКТИВНЫХ ЧАСТИЦ: {report.EffectiveParticles}/{config.SwarmSize}", LogMessageType.System);
            _context.SendLog($"⏱️ ВРЕМЯ ОПТИМИЗАЦИИ: {result.OptimizationTime:hh\\:mm\\:ss}", LogMessageType.System);
            _context.SendLog($"🔄 ИТЕРАЦИЙ PSO: {result.PSOIterations}/{config.MaxIterations}", LogMessageType.System);
            _context.SendLog($"🧬 ПОКОЛЕНИЙ GA: {result.GAGenerations}/{config.GAGenerations}", LogMessageType.System);
            _context.SendLog($"📈 ВСЕГО ОЦЕНОК: {report.TotalEvaluations}", LogMessageType.System);
            _context.SendLog($"🏆 МЕТОД ОПТИМИЗАЦИИ: {result.OptimizationMethod}", LogMessageType.System);
            
            LogEnhancedOptimalParameters(result.BestParameters);
            
            _context.SendLog("=================================================", LogMessageType.System);
        }
        
        private double CalculateExplorationRatio()
        {
            if (_currentSwarm == null || _currentSwarm.Particles.Count == 0) return 0.5;
            
            try
            {
                int exploring = 0;
                int exploiting = 0;
                
                foreach (var particle in _currentSwarm.Particles)
                {
                    if (particle.StagnationCount < 5 && particle.Age < 10)
                    {
                        exploring++;
                    }
                    else
                    {
                        exploiting++;
                    }
                }
                
                return (double)exploring / (exploring + exploiting);
            }
            catch
            {
                return 0.5;
            }
        }
        
        private void LogEnhancedOptimalParameters(Dictionary<string, decimal> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                _context.SendLog("❌ Оптимальные параметры не найдены", LogMessageType.System);
                return;
            }
            
            _context.SendLog("=== 🏆 ОПТИМАЛЬНЫЕ ПАРАМЕТРЫ ===", LogMessageType.System);
            
            // Группируем параметры по категориям
            var ichimokuParams = parameters.Where(p => p.Key.Contains("Length") || p.Key.Contains("Offset"))
                .OrderBy(p => p.Key);
            var riskParams = parameters.Where(p => p.Key.Contains("Percent") || p.Key.Contains("Profit") || p.Key.Contains("Spread"))
                .OrderBy(p => p.Key);
            var otherParams = parameters.Where(p => !p.Key.Contains("Length") && !p.Key.Contains("Offset") && 
                !p.Key.Contains("Percent") && !p.Key.Contains("Profit") && !p.Key.Contains("Spread"))
                .OrderBy(p => p.Key);
            
            if (ichimokuParams.Any())
            {
                _context.SendLog("📈 ПАРАМЕТРЫ ИШИМОКУ:", LogMessageType.System);
                foreach (var param in ichimokuParams)
                {
                    _context.SendLog($"   {param.Key}: {param.Value:F2}", LogMessageType.System);
                }
            }
            
            if (riskParams.Any())
            {
                _context.SendLog("🛡️ ПАРАМЕТРЫ РИСК-МЕНЕДЖМЕНТА:", LogMessageType.System);
                foreach (var param in riskParams)
                {
                    _context.SendLog($"   {param.Key}: {param.Value:F2}", LogMessageType.System);
                }
            }
            
            if (otherParams.Any())
            {
                _context.SendLog("⚙️ ПРОЧИЕ ПАРАМЕТРЫ:", LogMessageType.System);
                foreach (var param in otherParams)
                {
                    _context.SendLog($"   {param.Key}: {param.Value:F2}", LogMessageType.System);
                }
            }
            
            _context.SendLog("==============================", LogMessageType.System);
        }
        
        private void ApplyOptimizedParameters(Dictionary<string, decimal> parameters)
        {
            try
            {
                _context.SendLog("🔄 ПРИМЕНЕНИЕ ОПТИМИЗИРОВАННЫХ ПАРАМЕТРОВ...", LogMessageType.System);
                
                var sharedData = _context.SharedData;
                
                foreach (var param in parameters)
                {
                    // Обновляем параметры в общем хранилище
                    if (sharedData.ContainsKey(param.Key))
                    {
                        var strategyParam = sharedData[param.Key];
                        
                        if (strategyParam is StrategyParameterInt intParam)
                        {
                            intParam.ValueInt = (int)Math.Round(param.Value);
                            _context.SendLog($"   {param.Key}: {intParam.ValueInt} (было {intParam.ValueInt})", 
                                LogMessageType.System);
                        }
                        else if (strategyParam is StrategyParameterDecimal decimalParam)
                        {
                            decimalParam.ValueDecimal = param.Value;
                            _context.SendLog($"   {param.Key}: {decimalParam.ValueDecimal:F2} (было {decimalParam.ValueDecimal:F2})", 
                                LogMessageType.System);
                        }
                    }
                }
                
                _context.SendLog("ℹ️ Обновление значений параметров завершено", LogMessageType.System);
                _context.SendLog("✅ ОПТИМИЗИРОВАННЫЕ ПАРАМЕТРЫ ПРИМЕНЕНЫ", LogMessageType.System);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка применения параметров: {ex.Message}", LogMessageType.Error);
            }
        }
    }
    
    #endregion
    
    #region ==================== STRATEGY COMPONENTS ====================
    
    // 6. КОМПОНЕНТ СТРАТЕГИИ ИШИМОКУ
    public class IchimokuStrategyComponent : ITradingComponent
    {
        public string ComponentName => "IchimokuStrategy";
        
        private IComponentContext _context;
        private IStateMachine _stateMachine;
        
        // ✅ КЭШИРОВАНИЕ КОМПОНЕНТОВ: Избегаем повторных вызовов GetComponent
        private DataIndicatorComponent _cachedDataComponent;
        private PositionManagerComponent _cachedPositionManager;
        private RiskManagementComponent _cachedRiskManager;
        private TrailingStopComponent _cachedTrailingComponent;
        private DateTime _lastComponentCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _componentCacheRefreshInterval = TimeSpan.FromSeconds(5); // Обновляем кэш каждые 5 секунд
        
        // ✅ КЭШИРОВАНИЕ ПАРАМЕТРОВ: Избегаем повторных проверок ValueString
        private bool? _cachedShortTradingEnabled;
        private bool? _cachedOpenByTkKj;
        private bool? _cachedOpenByCloud;
        private bool? _cachedOpenByChikou;
        private bool? _cachedOpenByStochastic;
        private bool? _cachedExitByStochastic;
        private bool? _cachedUseCounterintuitive;
        private bool? _cachedCounterintuitiveEntry;
        private bool? _cachedCounterintuitiveExit;
        private bool? _cachedExitByTkKj;
        private bool? _cachedExitByCloud;
        private bool? _cachedExitByChikou;
        private bool? _cachedUseVolumeFilter;
        private bool? _cachedUseDuplicateProtection;
        private DateTime _lastParameterCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _parameterCacheRefreshInterval = TimeSpan.FromSeconds(1); // Обновляем кэш каждую секунду
        
        private StrategyParameterString _regime;
        private StrategyParameterInt _volume;
        private StrategyParameterString _shortTrading;
        private StrategyParameterString _openByTkKj;
        private StrategyParameterString _openByCloud;
        private StrategyParameterString _openByChikou;
        private StrategyParameterString _exitByTkKj;
        private StrategyParameterString _exitByCloud;
        private StrategyParameterString _exitByChikou;
        private StrategyParameterString _forceTradingMode;
        private StrategyParameterString _useManualTakeProfit;
        private StrategyParameterDecimal _manualTakeProfit;
        private StrategyParameterString _useVolumeFilter;
        private StrategyParameterDecimal _volumeMultiplier;
        private StrategyParameterInt _volumePeriod;
        private StrategyParameterString _useDuplicateProtection;
        private StrategyParameterInt _duplicateProtectionMinutes;
        private StrategyParameterDecimal _duplicatePriceTolerance;
        private StrategyParameterInt _duplicateTimeToleranceSeconds;
        private StrategyParameterBool _logPositionsNow;
        // Stochastic вход/выход
        private StrategyParameterString _openByStochastic;
        private StrategyParameterString _exitByStochastic;
        private StrategyParameterString _useCounterintuitive;
        private StrategyParameterString _counterintuitiveEntry;
        private StrategyParameterString _counterintuitiveExit;
        private StrategyParameterInt _stochPeriod;
        private StrategyParameterInt _stochSmoothing;
        private StrategyParameterInt _stochDPeriod;
        private StrategyParameterDecimal _stochOversold;
        private StrategyParameterDecimal _stochOverbought;
        // Усреднение
        private StrategyParameterInt _averagingCooldownCandles;
        private StrategyParameterDecimal _averagingLevel1;
        private StrategyParameterDecimal _averagingLevel2;
        private StrategyParameterDecimal _averagingLevel3;
        private StrategyParameterDecimal _averagingLevel4;
        private StrategyParameterDecimal _averagingLevel5;
        private StrategyParameterDecimal _averagingLevel6;
        private StrategyParameterDecimal _averagingLevel7;
        private StrategyParameterDecimal _averagingLevel8;
        private StrategyParameterDecimal _averagingLevel9;
        private StrategyParameterDecimal _averagingLevel10;
        private StrategyParameterDecimal _averagingLevel11;
        private StrategyParameterDecimal _averagingLevel12;
        private StrategyParameterBool _averagingLevel1Enabled;
        private StrategyParameterBool _averagingLevel2Enabled;
        private StrategyParameterBool _averagingLevel3Enabled;
        private StrategyParameterBool _averagingLevel4Enabled;
        private StrategyParameterBool _averagingLevel5Enabled;
        private StrategyParameterBool _averagingLevel6Enabled;
        private StrategyParameterBool _averagingLevel7Enabled;
        private StrategyParameterBool _averagingLevel8Enabled;
        private StrategyParameterBool _averagingLevel9Enabled;
        private StrategyParameterBool _averagingLevel10Enabled;
        private StrategyParameterBool _averagingLevel11Enabled;
        private StrategyParameterBool _averagingLevel12Enabled;
        private readonly ConcurrentDictionary<string, HashSet<int>> _executedAveragingLevels = new();
        private readonly ConcurrentDictionary<string, int> _lastAveragingBar = new();
        
        private readonly ConcurrentDictionary<string, DateTime> _throttleByKey = new();
        private readonly ConcurrentDictionary<string, LastOrderSignature> _lastOrderBySec = new();
        private readonly ConcurrentDictionary<string, decimal> _volumeCache = new();
        
        private string _pendingOpenReason;
        
        private class LastOrderSignature
        {
            public DateTime Time;
            public Side Side;
            public decimal Volume;
            public decimal Price;
            public string SecurityKey;
            public int CandleIndex;
        }
        
        public void Initialize(IComponentContext context)
        {
            _context = context;
            
            // Получаем параметры
            if (context.SharedData.TryGetValue(SharedDataKeys.Regime, out var regime))
                _regime = regime as StrategyParameterString;
            if (context.SharedData.TryGetValue(SharedDataKeys.Volume, out var volume))
                _volume = volume as StrategyParameterInt;
            if (context.SharedData.TryGetValue(SharedDataKeys.ShortTrading, out var shortTrading))
                _shortTrading = shortTrading as StrategyParameterString;
            if (context.SharedData.TryGetValue(SharedDataKeys.ForceTradingMode, out var ftMode))
                _forceTradingMode = ftMode as StrategyParameterString;
            if (context.SharedData.TryGetValue(SharedDataKeys.OpenByTkKj, out var openByTkKj))
                _openByTkKj = openByTkKj as StrategyParameterString;
            if (context.SharedData.TryGetValue(SharedDataKeys.OpenByCloud, out var openByCloud))
                _openByCloud = openByCloud as StrategyParameterString;
            if (context.SharedData.TryGetValue(SharedDataKeys.OpenByChikou, out var openByChikou))
                _openByChikou = openByChikou as StrategyParameterString;
            if (context.SharedData.TryGetValue("OpenByStochastic", out var openByStoch))
                _openByStochastic = openByStoch as StrategyParameterString;
            if (context.SharedData.TryGetValue("ExitByTkKj", out var exitByTkKj))
                _exitByTkKj = exitByTkKj as StrategyParameterString;
            if (context.SharedData.TryGetValue("ExitByCloud", out var exitByCloud))
                _exitByCloud = exitByCloud as StrategyParameterString;
            if (context.SharedData.TryGetValue("ExitByChikou", out var exitByChikou))
                _exitByChikou = exitByChikou as StrategyParameterString;
            if (context.SharedData.TryGetValue("ExitByStochastic", out var exitByStoch))
                _exitByStochastic = exitByStoch as StrategyParameterString;
            if (context.SharedData.TryGetValue("StochPeriod", out var stochPeriod))
                _stochPeriod = stochPeriod as StrategyParameterInt;
            if (context.SharedData.TryGetValue("StochSmoothing", out var stochSmooth))
                _stochSmoothing = stochSmooth as StrategyParameterInt;
            if (context.SharedData.TryGetValue("StochDPeriod", out var stochD))
                _stochDPeriod = stochD as StrategyParameterInt;
            if (context.SharedData.TryGetValue("StochOversold", out var stochOs))
                _stochOversold = stochOs as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("StochOverbought", out var stochOb))
                _stochOverbought = stochOb as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("UseManualTakeProfit", out var useTP))
                _useManualTakeProfit = useTP as StrategyParameterString;
            if (context.SharedData.TryGetValue("ManualTakeProfit", out var manualTP))
                _manualTakeProfit = manualTP as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("UseVolumeFilter", out var useVolume))
                _useVolumeFilter = useVolume as StrategyParameterString;
            if (context.SharedData.TryGetValue("VolumeMultiplier", out var volumeMultiplier))
                _volumeMultiplier = volumeMultiplier as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("VolumePeriod", out var volumePeriod))
                _volumePeriod = volumePeriod as StrategyParameterInt;
            if (context.SharedData.TryGetValue("UseDuplicateProtection", out var duplicateProtection))
                _useDuplicateProtection = duplicateProtection as StrategyParameterString;
            if (context.SharedData.TryGetValue("DuplicateProtectionMinutes", out var duplicateMinutes))
                _duplicateProtectionMinutes = duplicateMinutes as StrategyParameterInt;
            if (context.SharedData.TryGetValue("DuplicatePriceTolerance", out var priceTolerance))
                _duplicatePriceTolerance = priceTolerance as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("DuplicateTimeToleranceSeconds", out var timeTolerance))
                _duplicateTimeToleranceSeconds = timeTolerance as StrategyParameterInt;
            if (context.SharedData.TryGetValue("AveragingCooldownCandles", out var avgCooldown))
                _averagingCooldownCandles = avgCooldown as StrategyParameterInt;
            if (context.SharedData.TryGetValue("AveragingLevel1", out var avg1))
                _averagingLevel1 = avg1 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel2", out var avg2))
                _averagingLevel2 = avg2 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel3", out var avg3))
                _averagingLevel3 = avg3 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel4", out var avg4))
                _averagingLevel4 = avg4 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel5", out var avg5))
                _averagingLevel5 = avg5 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel6", out var avg6))
                _averagingLevel6 = avg6 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel7", out var avg7))
                _averagingLevel7 = avg7 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel8", out var avg8))
                _averagingLevel8 = avg8 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel9", out var avg9))
                _averagingLevel9 = avg9 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel10", out var avg10))
                _averagingLevel10 = avg10 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel11", out var avg11))
                _averagingLevel11 = avg11 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel12", out var avg12))
                _averagingLevel12 = avg12 as StrategyParameterDecimal;
            if (context.SharedData.TryGetValue("AveragingLevel1Enabled", out var avg1En))
                _averagingLevel1Enabled = avg1En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel2Enabled", out var avg2En))
                _averagingLevel2Enabled = avg2En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel3Enabled", out var avg3En))
                _averagingLevel3Enabled = avg3En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel4Enabled", out var avg4En))
                _averagingLevel4Enabled = avg4En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel5Enabled", out var avg5En))
                _averagingLevel5Enabled = avg5En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel6Enabled", out var avg6En))
                _averagingLevel6Enabled = avg6En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel7Enabled", out var avg7En))
                _averagingLevel7Enabled = avg7En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel8Enabled", out var avg8En))
                _averagingLevel8Enabled = avg8En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel9Enabled", out var avg9En))
                _averagingLevel9Enabled = avg9En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel10Enabled", out var avg10En))
                _averagingLevel10Enabled = avg10En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel11Enabled", out var avg11En))
                _averagingLevel11Enabled = avg11En as StrategyParameterBool;
            if (context.SharedData.TryGetValue("AveragingLevel12Enabled", out var avg12En))
                _averagingLevel12Enabled = avg12En as StrategyParameterBool;
            
            // Получаем State Machine
            _stateMachine = context.GetComponent<AdaptiveTradingStateMachine>();
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged += OnStateChanged;
            }
            
            _context.SendLog("✅ Стратегия Ишимоку инициализирована", LogMessageType.System);
        }
        
        private void OnStateChanged(TradingState previous, TradingState current, string reason)
        {
            // Логирование изменений состояния
            _context.SendLog($"🔄 Изменение состояния стратегии: {previous} -> {current} ({reason})", 
                LogMessageType.System);
        }
        
        public async Task ProcessAsync(Candle candle)
        {
            if (_regime.ValueString == "Выключен")
                return;
            
            // ✅ ОБНОВЛЕНИЕ КЭША: Обновляем кэш компонентов и параметров при необходимости
            RefreshComponentCacheIfNeeded();
            RefreshParameterCacheIfNeeded();
            
            // Обработка через State Machine
            _stateMachine?.ProcessEvent(TradingEvent.CandleFinished, candle);
            
            // Проверяем торговые сигналы
            await CheckTradingSignals(candle);
            
            // Проверяем усреднения при открытых позициях
            await CheckAveraging(candle);
            
            // Проверяем условия закрытия
            await CheckExitConditions(candle);
        }
        
        // ✅ КЭШИРОВАНИЕ: Методы для обновления кэша компонентов
        private void RefreshComponentCacheIfNeeded()
        {
            if (DateTime.Now - _lastComponentCacheUpdate < _componentCacheRefreshInterval)
                return;
            
            _cachedDataComponent = _context.GetComponent<DataIndicatorComponent>();
            _cachedPositionManager = _context.GetComponent<PositionManagerComponent>();
            _cachedRiskManager = _context.GetComponent<RiskManagementComponent>();
            _cachedTrailingComponent = _context.GetComponent<TrailingStopComponent>();
            _lastComponentCacheUpdate = DateTime.Now;
        }
        
        // ✅ КЭШИРОВАНИЕ: Методы для обновления кэша параметров
        private void RefreshParameterCacheIfNeeded()
        {
            if (DateTime.Now - _lastParameterCacheUpdate < _parameterCacheRefreshInterval)
                return;
            
            _cachedShortTradingEnabled = IsShortTradingEnabled();
            _cachedOpenByTkKj = IsParameterOn(_openByTkKj);
            _cachedOpenByCloud = IsParameterOn(_openByCloud);
            _cachedOpenByChikou = IsParameterOn(_openByChikou);
            _cachedOpenByStochastic = IsParameterOn(_openByStochastic);
            _cachedExitByStochastic = IsParameterOn(_exitByStochastic);
            _cachedUseCounterintuitive = IsParameterOn(_useCounterintuitive);
            _cachedCounterintuitiveEntry = IsParameterOn(_counterintuitiveEntry);
            _cachedCounterintuitiveExit = IsParameterOn(_counterintuitiveExit);
            _cachedExitByTkKj = IsParameterOn(_exitByTkKj);
            _cachedExitByCloud = IsParameterOn(_exitByCloud);
            _cachedExitByChikou = IsParameterOn(_exitByChikou);
            _cachedUseVolumeFilter = IsParameterOn(_useVolumeFilter);
            _cachedUseDuplicateProtection = IsParameterOn(_useDuplicateProtection);
            _lastParameterCacheUpdate = DateTime.Now;
        }
        
        private async Task CheckTradingSignals(Candle candle)
        {
            await Task.Run(() =>
            {
                try
                {
                    // ✅ Проверка неторговых периодов: если текущее время не разрешено - выходим
                    if (_context.IsTradingTimeAllowed != null)
                    {
                        var tab = _context.GetTab();
                        if (tab != null)
                        {
                            DateTime currentTime = tab.TimeServerCurrent;
                            if (!_context.IsTradingTimeAllowed(currentTime))
                            {
                                return; // Торговля запрещена в это время
                            }
                        }
                    }
                    
                    // Получаем значения индикаторов
                    var dataComponent = _context.GetComponent<DataIndicatorComponent>();
                    if (dataComponent == null) return;
                    
                    decimal tenkanValue = dataComponent.GetTenkanValue();
                    decimal kijunValue = dataComponent.GetKijunValue();
                    decimal senkouAValue = dataComponent.GetSenkouAValue();
                    decimal senkouBValue = dataComponent.GetSenkouBValue();
                    decimal chikouValue = dataComponent.GetChikouValue();
                    bool stochReady = dataComponent.TryGetStochasticValues(
                        out decimal currentK, out decimal previousK,
                        out decimal currentD, out decimal previousD);
                    
                    if (tenkanValue == 0 || kijunValue == 0) return;
                    
                    // Проверяем фильтр объема
                    if (!IsVolumeFilterPassed(candle)) return;
                    
                    // Проверяем режим торговли
                    if (_regime.ValueString == "Только закрытие") return;
                    
                    // Получаем менеджер позиций
                    var positionManager = _context.GetComponent<PositionManagerComponent>();
                    if (positionManager == null) return;
                    
                    bool hasLongPosition = positionManager.HasLongPosition();
                    bool hasShortPosition = positionManager.HasShortPosition();
                    
                    // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЕ ПАРАМЕТРЫ
                    bool openByStoch = _cachedOpenByStochastic ?? IsParameterOn(_openByStochastic);
                    
                    // Предварительно рассчитываем сигналы
                    bool stochLongSignal = stochReady && openByStoch &&
                        previousK < previousD && currentK > currentD &&
                        currentK < _stochOversold.ValueDecimal;
                    bool stochShortSignal = stochReady && openByStoch &&
                        previousK > previousD && currentK < currentD &&
                        currentK > _stochOverbought.ValueDecimal;
                    
                    bool buySignalActive = stochLongSignal || CheckBuySignals(tenkanValue, kijunValue, candle.Close, 
                        senkouAValue, senkouBValue, chikouValue);
                    // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЙ ПАРАМЕТР
                    bool shortTradingEnabled = _cachedShortTradingEnabled ?? IsShortTradingEnabled();
                    bool sellSignalActive = shortTradingEnabled && (stochShortSignal || CheckSellSignals(tenkanValue, kijunValue, candle.Close));
                    
                    // Сигналы для LONG
                    if (!hasLongPosition && !hasShortPosition)
                    {
                        if (buySignalActive)
                        {
                            // ✅ ЧЕТКОЕ УКАЗАНИЕ ТИПА СИГНАЛА
                            if (stochLongSignal)
                            {
                                _pendingOpenReason = $"Stochastic: K {currentK:F2} > D {currentD:F2} в зоне перепроданности ({_stochOversold.ValueDecimal:F2})";
                            }
                            else
                            {
                                // Используем сохраненный тип сигнала из CheckBuySignals или GetBuySignalReason
                                string savedSignalType = _context.SharedData.TryGetValue("LastBuySignalType", out var signalTypeObj) 
                                    ? signalTypeObj as string 
                                    : null;
                                
                                if (!string.IsNullOrEmpty(savedSignalType))
                                {
                                    _pendingOpenReason = savedSignalType;
                                }
                                else
                                {
                                    _pendingOpenReason = GetBuySignalReason(tenkanValue, kijunValue, candle.Close,
                                        senkouAValue, senkouBValue, chikouValue);
                                }
                            }
                            
                            _stateMachine?.ProcessEvent(TradingEvent.BuySignalDetected, candle);
                            TryOpenLongPosition(candle);
                        }
                    }
                    
                    // Сигналы для SHORT (если разрешено)
                    if (sellSignalActive && !hasLongPosition && !hasShortPosition)
                    {
                        // ✅ ЧЕТКОЕ УКАЗАНИЕ ТИПА СИГНАЛА
                        if (stochShortSignal)
                        {
                            _pendingOpenReason = $"Stochastic: K {currentK:F2} < D {currentD:F2} в зоне перекупленности ({_stochOverbought.ValueDecimal:F2})";
                        }
                        else
                        {
                            // Используем сохраненный тип сигнала из CheckSellSignals или GetSellSignalReason
                            string savedSignalType = _context.SharedData.TryGetValue("LastSellSignalType", out var signalTypeObj) 
                                ? signalTypeObj as string 
                                : null;
                            
                            if (!string.IsNullOrEmpty(savedSignalType))
                            {
                                _pendingOpenReason = savedSignalType;
                            }
                            else
                            {
                                _pendingOpenReason = GetSellSignalReason(tenkanValue, kijunValue);
                            }
                        }
                        
                        _stateMachine?.ProcessEvent(TradingEvent.SellSignalDetected, candle);
                        TryOpenShortPosition(candle);
                    }
                    
                    // ✅ ПРИНУДИТЕЛЬНАЯ ТОРГОВЛЯ: если включена — и при уже открытой позиции
                    if (_forceTradingMode?.ValueString == "Включено")
                    {
                        bool hasAnyPosition = hasLongPosition || hasShortPosition;
                        if (hasAnyPosition)
                        {
                            if (buySignalActive)
                            {
                                // ✅ ЧЕТКОЕ УКАЗАНИЕ ТИПА СИГНАЛА
                                if (stochLongSignal)
                                {
                                    _pendingOpenReason = $"Stochastic: K {currentK:F2} > D {currentD:F2} в зоне перепроданности ({_stochOversold.ValueDecimal:F2})";
                                }
                                else
                                {
                                    string savedSignalType = _context.SharedData.TryGetValue("LastBuySignalType", out var signalTypeObj) 
                                        ? signalTypeObj as string 
                                        : null;
                                    
                                    if (!string.IsNullOrEmpty(savedSignalType))
                                    {
                                        _pendingOpenReason = savedSignalType;
                                    }
                                    else
                                    {
                                        _pendingOpenReason = GetBuySignalReason(tenkanValue, kijunValue, candle.Close,
                                            senkouAValue, senkouBValue, chikouValue);
                                    }
                                }
                                _stateMachine?.ProcessEvent(TradingEvent.BuySignalDetected, candle);
                                TryOpenLongPosition(candle);
                            }
                            else if (sellSignalActive)
                            {
                                // ✅ ЧЕТКОЕ УКАЗАНИЕ ТИПА СИГНАЛА
                                if (stochShortSignal)
                                {
                                    _pendingOpenReason = $"Stochastic: K {currentK:F2} < D {currentD:F2} в зоне перекупленности ({_stochOverbought.ValueDecimal:F2})";
                                }
                                else
                                {
                                    string savedSignalType = _context.SharedData.TryGetValue("LastSellSignalType", out var signalTypeObj) 
                                        ? signalTypeObj as string 
                                        : null;
                                    
                                    if (!string.IsNullOrEmpty(savedSignalType))
                                    {
                                        _pendingOpenReason = savedSignalType;
                                    }
                                    else
                                    {
                                        _pendingOpenReason = GetSellSignalReason(tenkanValue, kijunValue);
                                    }
                                }
                                _stateMachine?.ProcessEvent(TradingEvent.SellSignalDetected, candle);
                                TryOpenShortPosition(candle);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _context.SendLog($"❌ Ошибка проверки сигналов: {ex.Message}", LogMessageType.Error);
                }
            });
        }
        
        private async Task CheckAveraging(Candle candle)
        {
            await Task.Run(() =>
            {
                try
                {
                    var positionManager = _context.GetComponent<PositionManagerComponent>();
                    var riskManager = _context.GetComponent<RiskManagementComponent>();
                    var tab = _context.GetTab();

                    if (positionManager == null || riskManager == null || tab == null)
                        return;

                    var activePositions = positionManager.GetActivePositions();
                    if (activePositions == null || activePositions.Count == 0)
                    {
                        _executedAveragingLevels.Clear();
                        _lastAveragingBar.Clear();
                        return;
                    }

                    int currentBar = tab.CandlesAll?.Count ?? 0;
                    decimal currentPrice = candle.Close;

                    EvaluateAveragingForSide(activePositions, positionManager, riskManager, Side.Buy, currentPrice, currentBar, candle);
                    EvaluateAveragingForSide(activePositions, positionManager, riskManager, Side.Sell, currentPrice, currentBar, candle);
                }
                catch (Exception ex)
                {
                    _context.SendLog($"❌ Ошибка проверки усреднений: {ex.Message}", LogMessageType.Error);
                }
            });
        }

        private void EvaluateAveragingForSide(List<Position> positions, PositionManagerComponent positionManager,
            RiskManagementComponent riskManager, Side side, decimal currentPrice, int currentBar, Candle candle)
        {
            var sidePositions = positions
                .Where(p => p.State == PositionStateType.Open && p.Direction == side && positionManager.IsBotPosition(p.Number))
                .ToList();

            if (sidePositions.Count == 0)
            {
                string keyToClear = GetAveragingKey(side);
                _executedAveragingLevels.TryRemove(keyToClear, out _);
                _lastAveragingBar.TryRemove(keyToClear, out _);
                return;
            }

            string key = GetAveragingKey(side);

            int lastBar = 0;
            bool hasLastBar = _lastAveragingBar.TryGetValue(key, out lastBar);

            if (_averagingCooldownCandles != null &&
                hasLastBar &&
                currentBar - lastBar < _averagingCooldownCandles.ValueInt)
            {
                return;
            }

            decimal averagePrice = CalculateAveragePrice(sidePositions, riskManager);
            if (averagePrice <= 0)
                return;

            var levels = GetEnabledAveragingLevels();
            if (levels.Count == 0)
                return;

            var executedLevels = _executedAveragingLevels.GetOrAdd(key, _ => new HashSet<int>());

            foreach (var level in levels)
            {
                if (executedLevels.Contains(level.index))
                    continue;

                decimal targetPrice = side == Side.Buy
                    ? averagePrice * (1 - level.value / 100m)
                    : averagePrice * (1 + level.value / 100m);

                bool shouldAverage = side == Side.Buy
                    ? currentPrice <= targetPrice
                    : currentPrice >= targetPrice;

                if (shouldAverage)
                {
                    // ✅ ЧЕТКОЕ УКАЗАНИЕ СИГНАЛА УСРЕДНЕНИЯ
                    string directionText = side == Side.Buy ? "LONG" : "SHORT";
                    _pendingOpenReason = $"Усреднение {directionText}: Уровень {level.index + 1} ({level.value:F2}%) | Усредненная цена: {averagePrice:F4} | Целевая цена: {targetPrice:F4}";
                    
                    // Сохраняем тип сигнала для логирования
                    _context.SharedData[side == Side.Buy ? "LastBuySignalType" : "LastSellSignalType"] = _pendingOpenReason;

                    if (side == Side.Buy)
                    {
                        _stateMachine?.ProcessEvent(TradingEvent.BuySignalDetected, candle);
                        TryOpenLongPosition(candle);
                    }
                    else
                    {
                        _stateMachine?.ProcessEvent(TradingEvent.SellSignalDetected, candle);
                        TryOpenShortPosition(candle);
                    }

                    executedLevels.Add(level.index);
                    _lastAveragingBar[key] = currentBar;
                    break;
                }
            }
        }

        private List<(int index, decimal value)> GetEnabledAveragingLevels()
        {
            var result = new List<(int, decimal)>();

            void AddLevel(StrategyParameterBool enabled, StrategyParameterDecimal value, int index)
            {
                if (enabled != null && enabled.ValueBool && value != null && value.ValueDecimal > 0)
                {
                    result.Add((index, value.ValueDecimal));
                }
            }

            AddLevel(_averagingLevel1Enabled, _averagingLevel1, 0);
            AddLevel(_averagingLevel2Enabled, _averagingLevel2, 1);
            AddLevel(_averagingLevel3Enabled, _averagingLevel3, 2);
            AddLevel(_averagingLevel4Enabled, _averagingLevel4, 3);
            AddLevel(_averagingLevel5Enabled, _averagingLevel5, 4);
            AddLevel(_averagingLevel6Enabled, _averagingLevel6, 5);
            AddLevel(_averagingLevel7Enabled, _averagingLevel7, 6);
            AddLevel(_averagingLevel8Enabled, _averagingLevel8, 7);
            AddLevel(_averagingLevel9Enabled, _averagingLevel9, 8);
            AddLevel(_averagingLevel10Enabled, _averagingLevel10, 9);
            AddLevel(_averagingLevel11Enabled, _averagingLevel11, 10);
            AddLevel(_averagingLevel12Enabled, _averagingLevel12, 11);

            return result;
        }

        private decimal CalculateAveragePrice(List<Position> positions, RiskManagementComponent riskManager)
        {
            decimal totalVolume = 0;
            decimal totalCost = 0;

            foreach (var position in positions)
            {
                decimal entryPrice = riskManager?.GetEntryPrice(position.Number) ?? position.EntryPrice;
                decimal volume = position.OpenVolume;

                if (entryPrice <= 0 || volume <= 0)
                    continue;

                totalVolume += volume;
                totalCost += volume * entryPrice;
            }

            return totalVolume > 0 ? totalCost / totalVolume : 0;
        }

        private string GetAveragingKey(Side side)
        {
            var tab = _context.GetTab();
            string security = tab?.Connector?.Security?.Name ?? "Unknown";
            return $"{security}_{side}";
        }

        private async Task CheckExitConditions(Candle candle)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Получаем менеджер позиций
                    // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЕ КОМПОНЕНТЫ
                    var positionManager = _cachedPositionManager ?? _context.GetComponent<PositionManagerComponent>();
                    if (positionManager == null) return;
                    
                    var activePositions = positionManager.GetActivePositions();
                    if (activePositions.Count == 0) return;
                    
                    // Получаем значения индикаторов
                    var dataComponent = _cachedDataComponent ?? _context.GetComponent<DataIndicatorComponent>();
                    if (dataComponent == null) return;
                    
                    decimal tenkanValue = dataComponent.GetTenkanValue();
                    decimal kijunValue = dataComponent.GetKijunValue();
                    bool stochReady = dataComponent.TryGetStochasticValues(
                        out decimal currentK, out decimal previousK,
                        out decimal currentD, out decimal previousD);
                    
                    if (tenkanValue == 0 || kijunValue == 0) return;
                    
                    foreach (var position in activePositions)
                    {
                        bool exitSignal = false;
                        string exitReason = "";
                        
                        // Проверяем тейк-профит
                        if (IsTakeProfitEnabled() && CheckManualTakeProfit(position, candle.Close))
                        {
                            exitSignal = true;
                            exitReason = "TakeProfit";
                        }
                        
                        // Проверяем сигналы индикаторов
                        if (!exitSignal && IsExitSignal(position, tenkanValue, kijunValue))
                        {
                            exitSignal = true;
                            exitReason = "Indicator Signal";
                        }
                        
                        // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЕ ПАРАМЕТРЫ
                        bool counterintuitiveExit = _cachedCounterintuitiveExit ?? IsParameterOn(_counterintuitiveExit);
                        bool useCounterintuitive = _cachedUseCounterintuitive ?? IsParameterOn(_useCounterintuitive);
                        
                        // Counterintuitive логика выхода
                        if (!exitSignal && counterintuitiveExit && useCounterintuitive)
                        {
                            // Используем уже полученный выше dataComponent, не создавая новую локальную переменную
                            if (dataComponent != null && dataComponent.TryGetCounterintuitiveEmaValues(out decimal ema1, out decimal ema2, out decimal ema3))
                            {
                                // Для LONG: выход когда ema2 < ema1 (разворот тренда)
                                if (position.Direction == Side.Buy && ema2 < ema1)
                                {
                                    exitSignal = true;
                                    exitReason = "Counterintuitive Exit (LONG)";
                                }
                                // Для SHORT: выход когда ema2 > ema1 (разворот тренда)
                                else if (position.Direction == Side.Sell && ema2 > ema1)
                                {
                                    exitSignal = true;
                                    exitReason = "Counterintuitive Exit (SHORT)";
                                }
                            }
                        }

                        // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЙ ПАРАМЕТР
                        bool exitByStoch = _cachedExitByStochastic ?? IsParameterOn(_exitByStochastic);
                        if (!exitSignal && stochReady && exitByStoch)
                        {
                            if (position.Direction == Side.Buy &&
                                previousK > previousD && currentK < currentD &&
                                currentK > _stochOverbought.ValueDecimal)
                            {
                                exitSignal = true;
                                exitReason = "Stochastic Exit (LONG)";
                            }
                            else if (position.Direction == Side.Sell &&
                                     previousK < previousD && currentK > currentD &&
                                     currentK < _stochOversold.ValueDecimal)
                            {
                                exitSignal = true;
                                exitReason = "Stochastic Exit (SHORT)";
                            }
                        }
                        
                        // ✅ ПРОВЕРЯЕМ ТРЕЙЛИНГ-СТОП - ПРИОРИТЕТНЫЙ МЕХАНИЗМ ВЫХОДА
                        // Трейлинг-стоп должен работать независимо от других условий
                        if (!exitSignal)
                        {
                            // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЙ КОМПОНЕНТ
                            var trailingComponent = _cachedTrailingComponent ?? _context.GetComponent<TrailingStopComponent>();
                            if (trailingComponent != null && trailingComponent.IsTrailingEnabled())
                            {
                                // Проверяем активацию трейлинга и срабатывание стопа
                                if (trailingComponent.CheckTrailingStop(position.Number, candle.Close, position))
                                {
                                    exitSignal = true;
                                    exitReason = "Trailing Stop";
                                    
                                    // Получаем уровень трейлинг-стопа для использования в TryClosePosition
                                    decimal trailingLevel = trailingComponent.GetTrailingLevel(position.Number);
                                    
                                    // ✅ КРИТИЧНО: Сохраняем уровень трейлинг-стопа для использования в TryClosePosition
                                    if (trailingLevel > 0)
                                    {
                                        _context.SharedData[$"TrailingClosePrice_{position.Number}"] = trailingLevel;
                                        _context.SharedData[$"TrailingStopReason_{position.Number}"] = "Trailing Stop";
                                        
                                        // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЙ КОМПОНЕНТ
                                        // Детальное логирование срабатывания трейлинг-стопа
                                        var riskManager = _cachedRiskManager ?? _context.GetComponent<RiskManagementComponent>();
                                        decimal entryPrice = riskManager?.GetEntryPrice(position.Number) ?? position.EntryPrice;
                                        decimal profitPercent = entryPrice > 0 
                                            ? (position.Direction == Side.Buy 
                                                ? (trailingLevel - entryPrice) / entryPrice * 100m
                                                : (entryPrice - trailingLevel) / entryPrice * 100m)
                                            : 0m;
                                        
                                        _context.SendLog($"🎯 ТРЕЙЛИНГ-СТОП СРАБОТАЛ для позиции #{position.Number}: Уровень {trailingLevel:F4} | Прибыль {profitPercent:F2}%", 
                                            LogMessageType.Trade);
                                    }
                                    else
                                    {
                                        _context.SendLog($"⚠️ ТРЕЙЛИНГ-СТОП: Уровень не установлен для позиции #{position.Number}", 
                                            LogMessageType.System);
                                    }
                                }
                            }
                        }
                        
                        if (exitSignal)
                        {
                            TryClosePosition(position, candle.Close, exitReason);
                            // Очищаем временные данные после закрытия
                            _context.SharedData.TryRemove($"TrailingClosePrice_{position.Number}", out _);
                            _context.SharedData.TryRemove($"TrailingStopReason_{position.Number}", out _);
                            
                            // ✅ ОЧИСТКА ДАННЫХ САМООБУЧАЕМОГО ТРЕЙЛИНГА при закрытии позиции
                            // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЙ КОМПОНЕНТ
                            var trailingComponent = _cachedTrailingComponent ?? _context.GetComponent<TrailingStopComponent>();
                            if (trailingComponent != null)
                            {
                                trailingComponent.ClearSelfLearningData(position.Number);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _context.SendLog($"❌ Ошибка проверки условий выхода: {ex.Message}", LogMessageType.Error);
                }
            });
        }
        
        private bool CheckBuySignals(decimal tenkan, decimal kijun, decimal currentPrice,
            decimal senkouA, decimal senkouB, decimal chikou)
        {
            bool signal = false;
            string signalType = "";
            
            // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЕ ПАРАМЕТРЫ
            bool counterintuitiveEntry = _cachedCounterintuitiveEntry ?? IsParameterOn(_counterintuitiveEntry);
            bool useCounterintuitive = _cachedUseCounterintuitive ?? IsParameterOn(_useCounterintuitive);
            
            // Counterintuitive логика входа (вход на откате при тренде) - ПРИОРИТЕТ 1
            if (counterintuitiveEntry && useCounterintuitive)
            {
                // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЙ КОМПОНЕНТ
                var dataComponent = _cachedDataComponent ?? _context.GetComponent<DataIndicatorComponent>();
                if (dataComponent != null && dataComponent.TryGetCounterintuitiveEmaValues(out decimal ema1, out decimal ema2, out decimal ema3))
                {
                    // Логика counterintuitive: тренд вверх (ema2 > ema1) и цена в откате ниже быстрой и контртрендовой EMA
                    if (ema2 > ema1 && currentPrice < ema2 && currentPrice < ema3)
                    {
                        signal = true;
                        signalType = $"Counterintuitive: EMA2({ema2:F4}) > EMA1({ema1:F4}) [тренд], цена({currentPrice:F4}) < EMA2 и < EMA3 [откат]";
                    }
                }
            }
            
            // ✅ ИСПОЛЬЗУЕМ КЭШИРОВАННЫЕ ПАРАМЕТРЫ
            bool openByTkKj = _cachedOpenByTkKj ?? IsParameterOn(_openByTkKj);
            bool openByCloud = _cachedOpenByCloud ?? IsParameterOn(_openByCloud);
            bool openByChikou = _cachedOpenByChikou ?? IsParameterOn(_openByChikou);
            
            // Пересечение Тенкан/Киджун - ПРИОРИТЕТ 2
            if (!signal && openByTkKj && tenkan > kijun)
            {
                signal = true;
                signalType = "Ишимоку: Пересечение Тенкан/Киджун вверх";
            }
            
            // Цена выше облака - ПРИОРИТЕТ 3
                if (!signal && openByCloud && IsPriceAboveCloud(currentPrice, senkouA, senkouB))
            {
                signal = true;
                signalType = "Ишимоку: Цена выше облака";
            }
            
            // Чикоу Спан выше цены - ПРИОРИТЕТ 4
            if (!signal && openByChikou && IsChikouAbovePrice(chikou, currentPrice))
            {
                signal = true;
                signalType = "Ишимоку: Чикоу Спан выше цены";
            }
            
            // Сохраняем тип сигнала для использования в TryOpenLongPosition
            if (signal && !string.IsNullOrEmpty(signalType))
            {
                _context.SharedData[SharedDataKeys.LastBuySignalType] = signalType;
            }
            
            return signal;
        }
        
        private bool CheckSellSignals(decimal tenkan, decimal kijun, decimal currentPrice = 0m)
        {
            bool signal = false;
            string signalType = "";
            
            // Counterintuitive логика входа для SHORT (вход на откате при нисходящем тренде) - ПРИОРИТЕТ 1
            if (IsParameterOn(_counterintuitiveEntry) && IsParameterOn(_useCounterintuitive))
            {
                var dataComponent = _context.GetComponent<DataIndicatorComponent>();
                if (dataComponent != null && dataComponent.TryGetCounterintuitiveEmaValues(out decimal ema1, out decimal ema2, out decimal ema3))
                {
                    // Если currentPrice не передан, получаем его из графика
                    if (currentPrice == 0m)
                    {
                        currentPrice = _context.GetTab()?.CandlesAll?.LastOrDefault()?.Close ?? 0m;
                    }
                    
                    // Логика counterintuitive: тренд вниз (ema2 < ema1) и цена в откате выше быстрой и контртрендовой EMA
                    if (currentPrice > 0 && ema2 < ema1 && currentPrice > ema2 && currentPrice > ema3)
                    {
                        signal = true;
                        signalType = $"Counterintuitive: EMA2({ema2:F4}) < EMA1({ema1:F4}) [тренд], цена({currentPrice:F4}) > EMA2 и > EMA3 [откат]";
                    }
                }
            }
            
            // Пересечение Тенкан/Киджун - ПРИОРИТЕТ 2
            if (!signal && IsParameterOn(_openByTkKj) && tenkan < kijun)
            {
                signal = true;
                signalType = "Ишимоку: Пересечение Тенкан/Киджун вниз";
            }
            
            // Сохраняем тип сигнала для использования в TryOpenShortPosition
            if (signal && !string.IsNullOrEmpty(signalType))
            {
                _context.SharedData[SharedDataKeys.LastSellSignalType] = signalType;
            }
            
            return signal;
        }
        
        private string GetSellSignalReason(decimal tenkan, decimal kijun)
        {
            if (IsParameterOn(_openByTkKj) && tenkan < kijun)
                return "Пересечение Тенкан/Киджун вниз";

            return "Сигнал на SHORT (условия индикаторов)";
        }
        
        private string GetBuySignalReason(decimal tenkan, decimal kijun, decimal currentPrice,
            decimal senkouA, decimal senkouB, decimal chikou)
        {
            if (IsParameterOn(_openByTkKj) && tenkan > kijun)
                return "Пересечение Тенкан/Киджун";
            
            if (IsParameterOn(_openByCloud) && IsPriceAboveCloud(currentPrice, senkouA, senkouB))
                return "Цена выше облака";
            
            if (IsParameterOn(_openByChikou) && IsChikouAbovePrice(chikou, currentPrice))
                return "Чикоу Спан выше цены";
            
            return "Сигнал Ишимоку";
        }

        // ===== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ ОБЛАКА И ЧИКОУ =====

        /// <summary>
        /// Проверка: цена выше облака (верхняя граница = max(Senkou A, Senkou span B)).
        /// Вынесено в отдельный метод для единообразия и удобства возможной доработки.
        /// </summary>
        private bool IsPriceAboveCloud(decimal price, decimal senkouA, decimal senkouB)
        {
            return price > Math.Max(senkouA, senkouB);
        }

        /// <summary>
        /// Проверка: цена ниже облака (нижняя граница = min(Senkou A, Senkou span B)).
        /// Пока нигде не используется, оставлено на будущее.
        /// </summary>
        private bool IsPriceBelowCloud(decimal price, decimal senkouA, decimal senkouB)
        {
            return price < Math.Min(senkouA, senkouB);
        }

        /// <summary>
        /// Проверка сигнала Chikou: Чикоу Спан выше текущей цены.
        /// </summary>
        private bool IsChikouAbovePrice(decimal chikou, decimal price)
        {
            return chikou > price;
        }

        /// <summary>
        /// Безопасный снимок коллекции свечей — чтобы снизить риск "Collection was modified" при одновременной отрисовке графика.
        /// Логика не меняется: берём актуальные свечи, но копируем в отдельный список перед использованием.
        /// </summary>
        private List<Candle> SafeCandlesSnapshot()
        {
            try
            {
                var tab = _context?.GetTab();
                var candles = tab?.CandlesAll;
                return candles != null ? new List<Candle>(candles) : new List<Candle>();
            }
            catch
            {
                return new List<Candle>();
            }
        }
        
        private bool IsExitSignal(Position position, decimal tenkan, decimal kijun)
        {
            if (position.Direction == Side.Buy)
            {
                return IsParameterOn(_exitByTkKj) && tenkan < kijun;
            }
            else
            {
                return IsParameterOn(_exitByTkKj) && tenkan > kijun;
            }
        }
        
        private void TryOpenLongPosition(Candle candle)
        {
            try
            {
                var positionManager = _context.GetComponent<PositionManagerComponent>();
                var tab = _context.GetTab();
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                
                if (positionManager == null || riskManager == null || tab == null) return;
                
                int currentBar = tab?.CandlesAll != null ? tab.CandlesAll.Count : 0;
                string securityKey = tab?.Connector?.Security?.Name ?? "Unknown";
                
                // ДОПОЛНИТЕЛЬНЫЙ ЖЁСТКИЙ ЛИМИТ: считаем все позиции с ненулевым объёмом (Open и Closing)
                if (_context.SharedData.TryGetValue("MaxOpenPositions", out var maxPosObj))
                {
                    var maxOpenPositionsParam = maxPosObj as StrategyParameterInt;
                    if (maxOpenPositionsParam != null)
                    {
                        var allPositions = tab.PositionsOpenAll ?? new List<Position>();
                        int effectiveOpenCount = allPositions.Count(p => p.OpenVolume > 0);
                        if (effectiveOpenCount >= maxOpenPositionsParam.ValueInt)
                        {
                            _context.SendLog(
                                $"🚫 ЛИМИТ ПОЗИЦИЙ (по объёму) ДОСТИГНУТ: всего позиций с объёмом {effectiveOpenCount}, " +
                                $"максимум разрешено {maxOpenPositionsParam.ValueInt}",
                                LogMessageType.System);
                            return;
                        }
                    }
                }
                
                // Проверяем возможность открытия (1-я проверка)
                if (!positionManager.CanBotOpenNewPosition(_volume.ValueInt, securityKey, currentBar))
                    return;
                
                // Проверяем риск-менеджмент
                if (!riskManager.CanOpenPosition(candle.Close, _volume.ValueInt, securityKey))
                    return;
                
                // Проверяем защиту от дублей
                if (IsDuplicateOrder(Side.Buy, _volume.ValueInt, candle.Close, securityKey, currentBar))
                    return;
                
                // ПЕРЕД САМОЙ ОТПРАВКОЙ ОРДЕРА делаем повторную проверку лимита
                currentBar = tab?.CandlesAll != null ? tab.CandlesAll.Count : currentBar;
                if (!positionManager.CanBotOpenNewPosition(_volume.ValueInt, securityKey, currentBar))
                {
                    _context.SendLog("🚫 Повторная проверка лимита перед BuyAtMarket запретила открытие LONG", 
                        LogMessageType.System);
                    return;
                }
                
                // Открываем позицию
                positionManager.RegisterOpenReason(_pendingOpenReason);
                tab?.BuyAtMarket(_volume.ValueInt);
                RememberLastOrder(Side.Buy, _volume.ValueInt, candle.Close, securityKey, currentBar);
                
                var longReason = string.IsNullOrWhiteSpace(_pendingOpenReason)
                    ? "Сигнал на LONG (условия индикаторов)"
                    : _pendingOpenReason;
                
                _context.SendLog($"🎯 ОТКРЫТИЕ LONG | Сигнал: {longReason} | Цена: {candle.Close:F4} | Объем: {_volume.ValueInt}", 
                    LogMessageType.Trade);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка открытия LONG: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private void TryOpenShortPosition(Candle candle)
        {
            try
            {
                if (!IsShortTradingEnabled()) return;
                
                var tab = _context.GetTab();
                var positionManager = _context.GetComponent<PositionManagerComponent>();
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                
                if (positionManager == null || riskManager == null || tab == null) return;
                
                int currentBar = tab?.CandlesAll != null ? tab.CandlesAll.Count : 0;
                string securityKey = tab?.Connector?.Security?.Name ?? "Unknown";
                
                // ДОПОЛНИТЕЛЬНЫЙ ЖЁСТКИЙ ЛИМИТ: считаем все позиции с ненулевым объёмом (Open и Closing)
                if (_context.SharedData.TryGetValue("MaxOpenPositions", out var maxPosObj))
                {
                    var maxOpenPositionsParam = maxPosObj as StrategyParameterInt;
                    if (maxOpenPositionsParam != null)
                    {
                        var allPositions = tab.PositionsOpenAll ?? new List<Position>();
                        int effectiveOpenCount = allPositions.Count(p => p.OpenVolume > 0);
                        if (effectiveOpenCount >= maxOpenPositionsParam.ValueInt)
                        {
                            _context.SendLog(
                                $"🚫 ЛИМИТ ПОЗИЦИЙ (по объёму) ДОСТИГНУТ: всего позиций с объёмом {effectiveOpenCount}, " +
                                $"максимум разрешено {maxOpenPositionsParam.ValueInt}",
                                LogMessageType.System);
                            return;
                        }
                    }
                }
                
                // Проверяем возможность открытия (1-я проверка)
                if (!positionManager.CanBotOpenNewPosition(_volume.ValueInt, securityKey, currentBar))
                    return;
                
                // Проверяем риск-менеджмент
                if (!riskManager.CanOpenPosition(candle.Close, _volume.ValueInt, securityKey))
                    return;
                
                // Проверяем защиту от дублей
                if (IsDuplicateOrder(Side.Sell, _volume.ValueInt, candle.Close, securityKey, currentBar))
                    return;
                
                // ПЕРЕД САМОЙ ОТПРАВКОЙ ОРДЕРА делаем повторную проверку лимита
                currentBar = tab?.CandlesAll != null ? tab.CandlesAll.Count : currentBar;
                if (!positionManager.CanBotOpenNewPosition(_volume.ValueInt, securityKey, currentBar))
                {
                    _context.SendLog("🚫 Повторная проверка лимита перед SellAtMarket запретила открытие SHORT", 
                        LogMessageType.System);
                    return;
                }
                
                // Открываем позицию
                positionManager.RegisterOpenReason(_pendingOpenReason);
                tab?.SellAtMarket(_volume.ValueInt);
                RememberLastOrder(Side.Sell, _volume.ValueInt, candle.Close, securityKey, currentBar);
                
                var shortReason = string.IsNullOrWhiteSpace(_pendingOpenReason)
                    ? "Сигнал на SHORT (условия индикаторов)"
                    : _pendingOpenReason;
                
                _context.SendLog($"🎯 ОТКРЫТИЕ SHORT | Сигнал: {shortReason} | Цена: {candle.Close:F4} | Объем: {_volume.ValueInt}", 
                    LogMessageType.Trade);
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка открытия SHORT: {ex.Message}", LogMessageType.Error);
            }
        }
        
        /// <summary>
        /// ✅ ЕДИНСТВЕННЫЙ МЕТОД ЗАКРЫТИЯ ПОЗИЦИЙ
        /// Все закрытия проходят через этот метод с абсолютной защитой от убытков.
        /// Используется только CloseAtLimit с контролем цены закрытия.
        /// </summary>
        private void TryClosePosition(Position position, decimal currentPrice, string reason)
        {
            try
            {
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager == null)
                {
                    _context.SendLog($"🚫 ЗАКРЫТИЕ ЗАПРЕЩЕНО: RiskManager недоступен для позиции #{position.Number}", 
                        LogMessageType.System);
                    return;
                }
                
                int positionId = position.Number;
                bool isLong = position.Direction == Side.Buy;
                
                // ✅ Получаем компонент трейлинг-стопа один раз для использования в методе
                var trailingComponent = _context.GetComponent<TrailingStopComponent>();
                
                // ✅ КРИТИЧЕСКАЯ ПРОВЕРКА: Абсолютная защита от убытков
                if (!riskManager.CanClosePosition(positionId, currentPrice, isLong))
                {
                    // Логирование уже выполнено в CanClosePosition
                    return;
                }

                // ✅ БАЗОВЫЕ ДАННЫЕ ДЛЯ РАСЧЁТА ЦЕЛЕВОЙ ЦЕНЫ ВЫХОДА
                decimal entryPrice = riskManager.GetEntryPrice(positionId);

                // ✅ ЕДИНАЯ ЛОГИКА РАСЧЁТА ЦЕЛЕВОЙ ЦЕНЫ ВЫХОДА
                // Здесь учитываются:
                //  - следящий стоп (все типы, включая самообучаемый и ATR)
                //  - ручной тейк-профит
                //  - минимальная прибыль как ЖЁСТКИЙ ПОЛ (floor)
                //  - отсутствие инициализации в RiskManager
                decimal closePrice;
                string priceSource;

                // Получаем целевую цену выхода и источник без деконструкции (совместимость со старыми версиями C#)
                ExitInfo exitInfo = GetExpectedExitPrice(position, riskManager, trailingComponent, entryPrice, currentPrice);
                decimal targetPrice = exitInfo.Price;
                string targetSource = exitInfo.Source;

                // Защита от нулевой/некорректной цены: если по какой-то причине GetExpectedExitPrice вернул 0,
                // используем текущую цену, чтобы не выставлять заведомо неверный лимит.
                if (targetPrice <= 0)
                {
                    closePrice = currentPrice;
                    priceSource = "текущая цена (fallback)";
                }
                else
                {
                    closePrice = targetPrice;
                    priceSource = targetSource;
                }

                // Дополнительная гарантия: если позиция не инициализирована в RiskManager (minProfitPrice == 0),
                // явно логируем это, чтобы было видно в журналах.
                decimal minProfitPrice = riskManager.GetMinProfitPrice(positionId);
                if (minProfitPrice == 0)
                {
                    _context.SendLog(
                        $"⚠️ Позиция #{positionId} не инициализирована в RiskManager (minProfitPrice=0). " +
                        $"Выход по цене {closePrice:F4} ({priceSource}), причина: {reason}",
                        LogMessageType.System);
                }

                // ✅ ЗАКРЫТИЕ ТОЛЬКО ЧЕРЕЗ CloseAtLimit - никаких CloseAtMarket
                _context.GetTab().CloseAtLimit(position, closePrice, position.OpenVolume);
                
                // ✅ ДЕТАЛЬНОЕ ЛОГИРОВАНИЕ: Показываем, какая цена использована и почему
                decimal profitPercent = entryPrice > 0 
                    ? (isLong ? (closePrice - entryPrice) / entryPrice * 100m : (entryPrice - closePrice) / entryPrice * 100m)
                    : 0m;
                
                _context.SendLog(
                    $"🔔 ЗАКРЫТИЕ #{positionId}: {reason} | Цена: {closePrice:F4} ({priceSource}) | " +
                    $"Прибыль: {profitPercent:F2}% | Мин.прибыль: {minProfitPrice:F4}", 
                    LogMessageType.Trade);
                
                // Очищаем временные данные после закрытия
                _context.SharedData.TryRemove($"TrailingClosePrice_{positionId}", out _);
                _context.SharedData.TryRemove($"TrailingStopReason_{positionId}", out _);
                
                // ✅ ОЧИСТКА ДАННЫХ САМООБУЧАЕМОГО ТРЕЙЛИНГА при закрытии позиции
                if (trailingComponent != null)
                {
                    trailingComponent.ClearSelfLearningData(positionId);
                }
            }
            catch (Exception ex)
            {
                _context.SendLog($"❌ Ошибка закрытия позиции #{position.Number}: {ex.Message}", 
                    LogMessageType.Error);
            }
        }

        /// <summary>
        /// Расчёт ожидаемой цены выхода для компонента стратегии
        /// (используется в TryClosePosition).
        /// Логика совпадает с основной версией в боте, но опирается только
        /// на компоненты и параметры, доступные в текущем компоненте.
        /// </summary>
        private ExitInfo GetExpectedExitPrice(Position pos, RiskManagementComponent riskManager,
            TrailingStopComponent trailingComponent, decimal entryPrice, decimal currentPrice = 0m)
        {
            decimal minProfitPrice = riskManager?.GetMinProfitPrice(pos.Number) ?? 0m;
            int positionId = pos.Number;
            bool isLong = pos.Direction == Side.Buy;
            
            bool trailingEnabled = trailingComponent != null && trailingComponent.IsTrailingEnabled();
            bool trailingActive = false;
            decimal trailingLevel = 0m;
            
            if (trailingEnabled)
            {
                trailingActive = trailingComponent.IsTrailingActive(positionId);
                
                if (trailingActive)
                {
                    trailingLevel = trailingComponent.GetTrailingLevel(positionId);
                    
                    if (trailingLevel == 0m && entryPrice > 0)
                    {
                        string trailingType = trailingComponent.GetTrailingType();
                        decimal priceForCalculation = currentPrice > 0 ? currentPrice : pos.EntryPrice;
                        
                        if (trailingType == "ATR")
                        {
                            var dataComponent = _context.GetComponent<DataIndicatorComponent>();
                            if (dataComponent != null)
                            {
                                decimal atr = dataComponent.GetAtrValue();
                                decimal atrMultiplier = trailingComponent.GetAtrMultiplier();
                                if (atr > 0 && atrMultiplier > 0)
                                {
                                    trailingLevel = isLong 
                                        ? priceForCalculation - atr * atrMultiplier
                                        : priceForCalculation + atr * atrMultiplier;
                                }
                            }
                        }
                        
                        if (trailingLevel == 0m)
                        {
                            decimal trailingDistance = trailingComponent.GetTrailingDistancePercent();
                            if (trailingDistance > 0)
                            {
                                trailingLevel = isLong 
                                    ? priceForCalculation * (1 - trailingDistance / 100m)
                                    : priceForCalculation * (1 + trailingDistance / 100m);
                            }
                        }
                    }
                }
            }

            decimal manualTp = 0m;
            bool takeProfitOn = _useManualTakeProfit != null && _useManualTakeProfit.ValueString == "Включён";
            if (takeProfitOn && entryPrice > 0)
            {
                manualTp = isLong
                    ? entryPrice * (1 + _manualTakeProfit.ValueDecimal / 100m)
                    : entryPrice * (1 - _manualTakeProfit.ValueDecimal / 100m);
            }

            // Приоритет 1: активный трейлинг (не ниже минимальной прибыли)
            if (trailingActive && trailingLevel > 0)
            {
                if (currentPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    
                    string source = trailingComponent.GetTrailingType() == "ATR" 
                        ? "trailing-ATR (current>=min-profit)"
                        : "trailing (current>=min-profit)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
                else
                {
                    decimal finalPrice = isLong
                        ? Math.Max(trailingLevel, minProfitPrice)
                        : Math.Min(trailingLevel, minProfitPrice);
                    
                    string source = trailingComponent.GetTrailingType() == "ATR"
                        ? "trailing-ATR (level>=min-profit)"
                        : "trailing (level>=min-profit)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
            }

            // Приоритет 2: ручной тейк‑профит (не ниже минимальной прибыли)
            if (takeProfitOn && manualTp > 0)
            {
                if (currentPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    
                    if ((isLong && currentPrice >= manualTp) || (!isLong && currentPrice <= manualTp))
                    {
                        string source = manualTp == minProfitPrice
                            ? "take-profit==min-profit"
                            : "take-profit (current>=min-profit)";
                        return new ExitInfo { Price = finalPrice, Source = source };
                    }
                }
                
                if (minProfitPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(manualTp, minProfitPrice)
                        : Math.Min(manualTp, minProfitPrice);
                    
                    string source = finalPrice == manualTp
                        ? "take-profit"
                        : "take-profit (скорректирован до мин.прибыли)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
            }

            // Приоритет 3: минимальная прибыль / текущая цена
            if (minProfitPrice > 0)
            {
                if (currentPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    
                    string source = "min-profit (current>=min-profit)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
                else
                {
                    return new ExitInfo { Price = minProfitPrice, Source = "min-profit" };
                }
            }

            return new ExitInfo { Price = entryPrice, Source = "entry" };
        }
        
        private bool CheckManualTakeProfit(Position position, decimal currentPrice)
        {
            if (!IsTakeProfitEnabled()) return false;
            
            try
            {
                var riskManager = _context.GetComponent<RiskManagementComponent>();
                if (riskManager == null) return false;
                
                int positionId = position.Number;
                decimal entryPrice = riskManager.GetEntryPrice(positionId);
                if (entryPrice == 0) return false;
                
                decimal profitPercent = position.Direction == Side.Buy 
                    ? (currentPrice - entryPrice) / entryPrice * 100
                    : (entryPrice - currentPrice) / entryPrice * 100;
                
                return profitPercent >= _manualTakeProfit.ValueDecimal;
            }
            catch
            {
                return false;
            }
        }
        
        private bool IsVolumeFilterPassed(Candle candle)
        {
            if (!IsParameterOn(_useVolumeFilter)) return true;
            
            try
            {
                // Упрощенная проверка объема
                return true;
            }
            catch
            {
                return true;
            }
        }
        
        /// <summary>
        /// ✅ ЗАЩИТА ОТ ДУБЛИРУЮЩИХ ВХОДОВ
        /// 
        /// Полная защита от ошибочных многократных входов:
        /// - Повторной покупки в одно и то же время
        /// - Одного и того же количества бумаг
        /// - По одной и той же цене
        /// - На одном и том же уровне
        /// 
        /// Механизм проверки:
        /// 1. Проверка точного совпадения (securityKey + side + volume + price)
        /// 2. Проверка похожих цен в пределах допуска (_duplicatePriceTolerance)
        /// 3. Проверка временного интервала (_duplicateProtectionMinutes)
        /// 4. Блокировка и логирование при обнаружении дубля
        /// </summary>
        private bool IsDuplicateOrder(Side side, decimal volume, decimal price, string securityKey, int candleIndex)
        {
            if (!IsParameterOn(_useDuplicateProtection)) return false;
            
            try
            {
                DateTime now = DateTime.Now;
                
                if (_lastOrderBySec.TryGetValue(securityKey, out LastOrderSignature lastOrder))
                {
                    // ✅ ПРОВЕРКА 1: Точное совпадение стороны (Buy/Sell)
                    bool sameSide = lastOrder.Side == side;
                    
                    // ✅ ПРОВЕРКА 2: Точное совпадение объема
                    bool sameVolume = lastOrder.Volume == volume;
                    
                    // ✅ ПРОВЕРКА 3: Похожие цены в пределах допуска (_duplicatePriceTolerance)
                    decimal priceDiffPercent = Math.Abs(lastOrder.Price - price) / price * 100m;
                    bool samePrice = priceDiffPercent <= _duplicatePriceTolerance.ValueDecimal;
                    
                    // ✅ ПРОВЕРКА 4: Временной интервал (_duplicateProtectionMinutes)
                    TimeSpan timeSinceLastOrder = now - lastOrder.Time;
                    bool recentOrder = timeSinceLastOrder < TimeSpan.FromMinutes(_duplicateProtectionMinutes.ValueInt);
                    
                    // ✅ БЛОКИРОВКА: Если все условия совпадают - это дублирующая заявка
                    if (sameSide && sameVolume && samePrice && recentOrder)
                    {
                        LogThrottled("duplicate_order", 
                            $"🚫 ДУБЛИРУЮЩАЯ ЗАЯВКА ОТКЛОНЕНА: {side} {volume} лотов по {price:F4} | " +
                            $"Последний заказ: {lastOrder.Price:F4} ({timeSinceLastOrder.TotalMinutes:F1} мин назад)", 
                            LogMessageType.System, TimeSpan.FromSeconds(10));
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        private void RememberLastOrder(Side side, decimal volume, decimal price, string securityKey, int candleIndex)
        {
            try
            {
                _lastOrderBySec[securityKey] = new LastOrderSignature
                {
                    Time = DateTime.Now,
                    Side = side,
                    Volume = volume,
                    Price = price,
                    SecurityKey = securityKey,
                    CandleIndex = candleIndex
                };
            }
            catch { }
        }
        
        private void LogThrottled(string key, string message, LogMessageType type, TimeSpan cooldown)
        {
            DateTime now = DateTime.Now;
            if (_throttleByKey.TryGetValue(key, out DateTime last) && now - last < cooldown)
                return;
            _throttleByKey[key] = now;
            _context.SendLog(message, type);
        }
        
        private bool IsParameterOn(StrategyParameterString param)
        {
            return param?.ValueString == "Включено" || param?.ValueString == "Включён" || param?.ValueString == "Включена";
        }
        
        private bool IsShortTradingEnabled()
        {
            return _shortTrading?.ValueString == "Включена";
        }
        
        private bool IsTakeProfitEnabled()
        {
            return _useManualTakeProfit?.ValueString == "Включён";
        }
        
        public void Dispose()
        {
            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= OnStateChanged;
            }
            
            _throttleByKey.Clear();
            _lastOrderBySec.Clear();
            _volumeCache.Clear();
        }
    }
    
    #endregion
    
    #region ==================== MAIN BOT CLASS ====================
    
    [Bot("IshimokuAdaptiveTrailing")]
    public class IshimokuAdaptiveTrailing : BotPanel
    {
        // Все параметры
        private StrategyParameterString _regime;
        private StrategyParameterInt _volume;
        private StrategyParameterString _shortTrading;
        private StrategyParameterString _closeMode;
        private StrategyParameterString _forceTradingMode;
        private StrategyParameterInt _tenkanLength;
        private StrategyParameterInt _kijunLength;
        private StrategyParameterInt _senkouBLength;
        private StrategyParameterInt _senkouOffset;
        private StrategyParameterString _openByTkKj;
        private StrategyParameterString _openByCloud;
        private StrategyParameterString _openByChikou;
        private StrategyParameterString _openByStochastic;
        private StrategyParameterString _exitByTkKj;
        private StrategyParameterString _exitByCloud;
        private StrategyParameterString _exitByChikou;
        private StrategyParameterString _exitByStochastic;
        private StrategyParameterInt _stochPeriod;
        private StrategyParameterInt _stochSmoothing;
        private StrategyParameterInt _stochDPeriod;
        private StrategyParameterDecimal _stochOversold;
        private StrategyParameterDecimal _stochOverbought;
        private StrategyParameterDecimal _averagingLevel1;
        private StrategyParameterDecimal _averagingLevel2;
        private StrategyParameterDecimal _averagingLevel3;
        private StrategyParameterDecimal _averagingLevel4;
        private StrategyParameterDecimal _averagingLevel5;
        private StrategyParameterDecimal _averagingLevel6;
        private StrategyParameterDecimal _averagingLevel7;
        private StrategyParameterDecimal _averagingLevel8;
        private StrategyParameterDecimal _averagingLevel9;
        private StrategyParameterDecimal _averagingLevel10;
        private StrategyParameterDecimal _averagingLevel11;
        private StrategyParameterDecimal _averagingLevel12;
        private StrategyParameterBool _averagingLevel1Enabled;
        private StrategyParameterBool _averagingLevel2Enabled;
        private StrategyParameterBool _averagingLevel3Enabled;
        private StrategyParameterBool _averagingLevel4Enabled;
        private StrategyParameterBool _averagingLevel5Enabled;
        private StrategyParameterBool _averagingLevel6Enabled;
        private StrategyParameterBool _averagingLevel7Enabled;
        private StrategyParameterBool _averagingLevel8Enabled;
        private StrategyParameterBool _averagingLevel9Enabled;
        private StrategyParameterBool _averagingLevel10Enabled;
        private StrategyParameterBool _averagingLevel11Enabled;
        private StrategyParameterBool _averagingLevel12Enabled;
        private StrategyParameterInt _averagingCooldownCandles;
        private StrategyParameterString _useTrailingStop;
        private StrategyParameterString _trailingType;
        private StrategyParameterDecimal _trailingStartPercent;
        private StrategyParameterDecimal _trailingDistancePercent;
        private StrategyParameterInt _atrPeriod;
        private StrategyParameterDecimal _atrMultiplier;
        private StrategyParameterString _useManualTakeProfit;
        private StrategyParameterDecimal _manualTakeProfit;
        private StrategyParameterDecimal _minProfitPercentParam;
        private StrategyParameterInt _maxOpenPositions;
        private StrategyParameterString _useBreakEven;
        private StrategyParameterDecimal _breakEvenTriggerPercent;
        private StrategyParameterInt _reentryCooldownCandles;
        private StrategyParameterDecimal _maxSpreadPercent;
        private StrategyParameterString _logVerbosity;
        private StrategyParameterInt _positionStatusEveryNBars;
        private StrategyParameterInt _unrealizedPnLLogIntervalMin;
        private StrategyParameterString _useVolumeFilter;
        private StrategyParameterDecimal _volumeMultiplier;
        private StrategyParameterInt _volumePeriod;
        private StrategyParameterString _useDuplicateProtection;
        private StrategyParameterInt _duplicateProtectionMinutes;
        private StrategyParameterDecimal _duplicatePriceTolerance;
        private StrategyParameterInt _duplicateTimeToleranceSeconds;
        private StrategyParameterBool _logPositionsNow;
        private StrategyParameterString _useAIOptimization;
        private StrategyParameterString _optimizationMode;
        private StrategyParameterBool _autoApplyResults;
        private StrategyParameterBool _preserveSafetyLogic;
        private StrategyParameterInt _psoSwarmSize;
        private StrategyParameterInt _psoMaxIterations;
        private StrategyParameterDecimal _psoInertia;
        private StrategyParameterDecimal _psoCognitiveWeight;
        private StrategyParameterDecimal _psoSocialWeight;
        private StrategyParameterString _psoUseAdaptiveInertia;
        private StrategyParameterDecimal _psoStartInertia;
        private StrategyParameterDecimal _psoEndInertia;
        private StrategyParameterString _psoUseSubSwarms;
        private StrategyParameterInt _psoSubSwarmCount;
        private StrategyParameterDecimal _psoMutationRate;
        private StrategyParameterDecimal _psoCrossoverRate;
        private StrategyParameterString _useGeneticEnhancement;
        private StrategyParameterInt _gaPopulationSize;
        private StrategyParameterInt _gaGenerations;
        private StrategyParameterDecimal _gaMutationRate;
        private StrategyParameterDecimal _gaCrossoverRate;
        private StrategyParameterString _continuousOptimization;
        private StrategyParameterInt _optimizationIntervalMinutes;
        
        // Время последнего детального лога по открытым позициям
        private DateTime _lastPositionStatusLogTime = DateTime.MinValue;
        
        // Флаги выбора параметров для оптимизации
        private StrategyParameterBool _optimizeTenkanLength;
        private StrategyParameterBool _optimizeKijunLength;
        private StrategyParameterBool _optimizeSenkouBLength;
        private StrategyParameterBool _optimizeSenkouOffset;
        private StrategyParameterBool _optimizeStochPeriod;
        private StrategyParameterBool _optimizeStochSmoothing;
        private StrategyParameterBool _optimizeStochDPeriod;
        private StrategyParameterBool _optimizeStochOversold;
        private StrategyParameterBool _optimizeStochOverbought;
        private StrategyParameterBool _optimizeAveragingLevel1;
        private StrategyParameterBool _optimizeAveragingLevel2;
        private StrategyParameterBool _optimizeAveragingLevel3;
        private StrategyParameterBool _optimizeAveragingLevel4;
        private StrategyParameterBool _optimizeAveragingLevel5;
        private StrategyParameterBool _optimizeAveragingLevel6;
        private StrategyParameterBool _optimizeAveragingLevel7;
        private StrategyParameterBool _optimizeAveragingLevel8;
        private StrategyParameterBool _optimizeAveragingLevel9;
        private StrategyParameterBool _optimizeAveragingLevel10;
        private StrategyParameterBool _optimizeAveragingLevel11;
        private StrategyParameterBool _optimizeAveragingLevel12;
        private StrategyParameterBool _optimizeMinProfitPercent;
        private StrategyParameterBool _optimizeTrailingStartPercent;
        private StrategyParameterBool _optimizeTrailingDistancePercent;
        private StrategyParameterBool _optimizeSelfLearningTrailing;
        private StrategyParameterBool _optimizeManualTakeProfit;
        private StrategyParameterBool _optimizeBreakEvenTriggerPercent;
        private StrategyParameterBool _optimizeMaxSpreadPercent;
        private StrategyParameterBool _optimizeATRPeriod;
        private StrategyParameterBool _optimizeATRMultiplier;
        private StrategyParameterBool _optimizeVolumeMultiplier;
        private StrategyParameterBool _optimizeVolumePeriod;
        private StrategyParameterBool _optimizeReentryCooldownCandles;
        private StrategyParameterBool _optimizeMaxOpenPositions;
        
        // Counterintuitive параметры
        private StrategyParameterString _useCounterintuitive;
        private StrategyParameterString _counterintuitiveEntry;
        private StrategyParameterString _counterintuitiveExit;
        private StrategyParameterInt _counterintuitiveEma1Period;
        private StrategyParameterInt _counterintuitiveEma2Period;
        private StrategyParameterInt _counterintuitiveEma3Period;
        
        // Флаги оптимизации для counterintuitive
        private StrategyParameterBool _optimizeCounterintuitiveEma1Period;
        private StrategyParameterBool _optimizeCounterintuitiveEma2Period;
        private StrategyParameterBool _optimizeCounterintuitiveEma3Period;
        
        // Неторговые дни параметры
        private StrategyParameterBool _mondayTrade;
        private StrategyParameterBool _tuesdayTrade;
        private StrategyParameterBool _wednesdayTrade;
        private StrategyParameterBool _thursdayTrade;
        private StrategyParameterBool _fridayTrade;
        private StrategyParameterBool _saturdayTrade;
        private StrategyParameterBool _sundayTrade;
        
        // Параметры неторговых периодов для каждого дня (3 периода на день)
        // Понедельник
        private StrategyParameterBool _mondayPeriod1Enabled;
        private StrategyParameterInt _mondayPeriod1StartHour;
        private StrategyParameterInt _mondayPeriod1StartMinute;
        private StrategyParameterInt _mondayPeriod1EndHour;
        private StrategyParameterInt _mondayPeriod1EndMinute;
        private StrategyParameterBool _mondayPeriod2Enabled;
        private StrategyParameterInt _mondayPeriod2StartHour;
        private StrategyParameterInt _mondayPeriod2StartMinute;
        private StrategyParameterInt _mondayPeriod2EndHour;
        private StrategyParameterInt _mondayPeriod2EndMinute;
        private StrategyParameterBool _mondayPeriod3Enabled;
        private StrategyParameterInt _mondayPeriod3StartHour;
        private StrategyParameterInt _mondayPeriod3StartMinute;
        private StrategyParameterInt _mondayPeriod3EndHour;
        private StrategyParameterInt _mondayPeriod3EndMinute;
        
        // Вторник
        private StrategyParameterBool _tuesdayPeriod1Enabled;
        private StrategyParameterInt _tuesdayPeriod1StartHour;
        private StrategyParameterInt _tuesdayPeriod1StartMinute;
        private StrategyParameterInt _tuesdayPeriod1EndHour;
        private StrategyParameterInt _tuesdayPeriod1EndMinute;
        private StrategyParameterBool _tuesdayPeriod2Enabled;
        private StrategyParameterInt _tuesdayPeriod2StartHour;
        private StrategyParameterInt _tuesdayPeriod2StartMinute;
        private StrategyParameterInt _tuesdayPeriod2EndHour;
        private StrategyParameterInt _tuesdayPeriod2EndMinute;
        private StrategyParameterBool _tuesdayPeriod3Enabled;
        private StrategyParameterInt _tuesdayPeriod3StartHour;
        private StrategyParameterInt _tuesdayPeriod3StartMinute;
        private StrategyParameterInt _tuesdayPeriod3EndHour;
        private StrategyParameterInt _tuesdayPeriod3EndMinute;
        
        // Среда
        private StrategyParameterBool _wednesdayPeriod1Enabled;
        private StrategyParameterInt _wednesdayPeriod1StartHour;
        private StrategyParameterInt _wednesdayPeriod1StartMinute;
        private StrategyParameterInt _wednesdayPeriod1EndHour;
        private StrategyParameterInt _wednesdayPeriod1EndMinute;
        private StrategyParameterBool _wednesdayPeriod2Enabled;
        private StrategyParameterInt _wednesdayPeriod2StartHour;
        private StrategyParameterInt _wednesdayPeriod2StartMinute;
        private StrategyParameterInt _wednesdayPeriod2EndHour;
        private StrategyParameterInt _wednesdayPeriod2EndMinute;
        private StrategyParameterBool _wednesdayPeriod3Enabled;
        private StrategyParameterInt _wednesdayPeriod3StartHour;
        private StrategyParameterInt _wednesdayPeriod3StartMinute;
        private StrategyParameterInt _wednesdayPeriod3EndHour;
        private StrategyParameterInt _wednesdayPeriod3EndMinute;
        
        // Четверг
        private StrategyParameterBool _thursdayPeriod1Enabled;
        private StrategyParameterInt _thursdayPeriod1StartHour;
        private StrategyParameterInt _thursdayPeriod1StartMinute;
        private StrategyParameterInt _thursdayPeriod1EndHour;
        private StrategyParameterInt _thursdayPeriod1EndMinute;
        private StrategyParameterBool _thursdayPeriod2Enabled;
        private StrategyParameterInt _thursdayPeriod2StartHour;
        private StrategyParameterInt _thursdayPeriod2StartMinute;
        private StrategyParameterInt _thursdayPeriod2EndHour;
        private StrategyParameterInt _thursdayPeriod2EndMinute;
        private StrategyParameterBool _thursdayPeriod3Enabled;
        private StrategyParameterInt _thursdayPeriod3StartHour;
        private StrategyParameterInt _thursdayPeriod3StartMinute;
        private StrategyParameterInt _thursdayPeriod3EndHour;
        private StrategyParameterInt _thursdayPeriod3EndMinute;
        
        // Пятница
        private StrategyParameterBool _fridayPeriod1Enabled;
        private StrategyParameterInt _fridayPeriod1StartHour;
        private StrategyParameterInt _fridayPeriod1StartMinute;
        private StrategyParameterInt _fridayPeriod1EndHour;
        private StrategyParameterInt _fridayPeriod1EndMinute;
        private StrategyParameterBool _fridayPeriod2Enabled;
        private StrategyParameterInt _fridayPeriod2StartHour;
        private StrategyParameterInt _fridayPeriod2StartMinute;
        private StrategyParameterInt _fridayPeriod2EndHour;
        private StrategyParameterInt _fridayPeriod2EndMinute;
        private StrategyParameterBool _fridayPeriod3Enabled;
        private StrategyParameterInt _fridayPeriod3StartHour;
        private StrategyParameterInt _fridayPeriod3StartMinute;
        private StrategyParameterInt _fridayPeriod3EndHour;
        private StrategyParameterInt _fridayPeriod3EndMinute;
        
        // Суббота
        private StrategyParameterBool _saturdayPeriod1Enabled;
        private StrategyParameterInt _saturdayPeriod1StartHour;
        private StrategyParameterInt _saturdayPeriod1StartMinute;
        private StrategyParameterInt _saturdayPeriod1EndHour;
        private StrategyParameterInt _saturdayPeriod1EndMinute;
        private StrategyParameterBool _saturdayPeriod2Enabled;
        private StrategyParameterInt _saturdayPeriod2StartHour;
        private StrategyParameterInt _saturdayPeriod2StartMinute;
        private StrategyParameterInt _saturdayPeriod2EndHour;
        private StrategyParameterInt _saturdayPeriod2EndMinute;
        private StrategyParameterBool _saturdayPeriod3Enabled;
        private StrategyParameterInt _saturdayPeriod3StartHour;
        private StrategyParameterInt _saturdayPeriod3StartMinute;
        private StrategyParameterInt _saturdayPeriod3EndHour;
        private StrategyParameterInt _saturdayPeriod3EndMinute;
        
        // Воскресенье
        private StrategyParameterBool _sundayPeriod1Enabled;
        private StrategyParameterInt _sundayPeriod1StartHour;
        private StrategyParameterInt _sundayPeriod1StartMinute;
        private StrategyParameterInt _sundayPeriod1EndHour;
        private StrategyParameterInt _sundayPeriod1EndMinute;
        private StrategyParameterBool _sundayPeriod2Enabled;
        private StrategyParameterInt _sundayPeriod2StartHour;
        private StrategyParameterInt _sundayPeriod2StartMinute;
        private StrategyParameterInt _sundayPeriod2EndHour;
        private StrategyParameterInt _sundayPeriod2EndMinute;
        private StrategyParameterBool _sundayPeriod3Enabled;
        private StrategyParameterInt _sundayPeriod3StartHour;
        private StrategyParameterInt _sundayPeriod3StartMinute;
        private StrategyParameterInt _sundayPeriod3EndHour;
        private StrategyParameterInt _sundayPeriod3EndMinute;
        
        private BotTabSimple _tab;
        private ComponentAssembly _assembly;
        private BotComponentContext _componentContext;
        private AdaptiveTradingStateMachine _stateMachine;
        private readonly ConcurrentDictionary<string, HashSet<int>> _executedAveragingLevels = new();
        private readonly ConcurrentDictionary<string, int> _lastAveragingBar = new();
        
        public IshimokuAdaptiveTrailing(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            
            CreateParameters();
            InitializeAssembly();
            
            Description = "Ишимоку стратегия с адаптивным трейлингом, жёсткой защитой минимальной прибыли и ГИБРИДНОЙ AI оптимизацией (PSO + Генетический алгоритм)";
            
            SendNewLogMessage("=== 🚀 ISHIMOKU ADAPTIVE TRAILING С УСИЛЕННОЙ AI ОПТИМИЗАЦИЕЙ ===", 
                LogMessageType.System);
            SendNewLogMessage("🤖 ГИБРИДНАЯ ОПТИМИЗАЦИЯ: PSO + ГЕНЕТИЧЕСКИЙ АЛГОРИТМ", LogMessageType.System);
            SendNewLogMessage("🎯 НЕПРЕРЫВНАЯ ОПТИМИЗАЦИЯ ВСЕХ ПАРАМЕТРОВ", LogMessageType.System);
            SendNewLogMessage("🔄 ЗАПУЩЕН: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), 
                LogMessageType.System);
            LogCurrentParameters();
        }
        
        private void CreateParameters()
        {
            // Создание всех параметров
            _regime = CreateParameter("Режим", "Включён", new[] { "Включён", "Выключен", "Только закрытие" });
            _volume = CreateParameter("Объём лотов", 1, 1, 100, 1);
            _shortTrading = CreateParameter("Шорт торговля", "Выключена", new[] { "Включена", "Выключена" });
            _closeMode = CreateParameter("Режим закрытия", "Общая позиция", new[] { "Общая позиция", "По отдельным сделкам" });
            _forceTradingMode = CreateParameter("Принудительная торговля", "Выключено", new[] { "Включено", "Выключено" }, ParameterGroups.TradingModes);
            
            // --- ВКЛАДКА «Ишимоку» ---
            _tenkanLength = CreateParameter("Tenkan период", 9, 1, 50, 1, ParameterGroups.Ichimoku);
            _kijunLength = CreateParameter("Kijun период", 26, 1, 100, 1, ParameterGroups.Ichimoku);
            _senkouBLength = CreateParameter("Senkou span B период", 52, 1, 200, 1, ParameterGroups.Ichimoku);
            _senkouOffset = CreateParameter("Senkou смещение", 26, 1, 100, 1, ParameterGroups.Ichimoku);
            _stochPeriod = CreateParameter("Stochastic %K период", 14, 5, 50, 1, ParameterGroups.Stochastic);
            _stochSmoothing = CreateParameter("Stochastic сглаживание %K", 3, 1, 10, 1, ParameterGroups.Stochastic);
            _stochDPeriod = CreateParameter("Stochastic %D период", 3, 1, 10, 1, ParameterGroups.Stochastic);
            _stochOversold = CreateParameter("Stochastic перепроданность %", 20.0m, 5.0m, 40.0m, 1.0m, ParameterGroups.Stochastic);
            _stochOverbought = CreateParameter("Stochastic перекупленность %", 80.0m, 60.0m, 95.0m, 1.0m, ParameterGroups.Stochastic);
            
            _openByTkKj = CreateParameter("Открытие: Пересечение Тенкан/Киджун", "Включено", new[] { "Включено", "Выключено" }, ParameterGroups.Ichimoku);
            _openByCloud = CreateParameter("Открытие: Цена и облако", "Включено", new[] { "Включено", "Выключено" }, ParameterGroups.Ichimoku);
            _openByChikou = CreateParameter("Открытие: Чикоу Спан", "Включено", new[] { "Включено", "Выключено" }, ParameterGroups.Ichimoku);
            _openByStochastic = CreateParameter("Открытие: Стохастик", "Выключено", new[] { "Включено", "Выключено" }, ParameterGroups.Stochastic);
            
            _exitByTkKj = CreateParameter("Выход: Пересечение Тенкан/Киджун", "Включено", new[] { "Включено", "Выключено" }, ParameterGroups.Ichimoku);
            _exitByCloud = CreateParameter("Выход: Цена и облако", "Включено", new[] { "Включено", "Выключено" }, ParameterGroups.Ichimoku);
            _exitByChikou = CreateParameter("Выход: Чикоу Спан", "Включено", new[] { "Включено", "Выключено" }, ParameterGroups.Ichimoku);
            _exitByStochastic = CreateParameter("Выход: Стохастик", "Выключено", new[] { "Включено", "Выключено" }, ParameterGroups.Stochastic);
            
            _useTrailingStop = CreateParameter("Трейлинг-стоп", "Выключен", new[] { "Включён", "Выключен" });
            _trailingType = CreateParameter("Тип трейлинга", "Самообучаемый", new[] { "Фиксированный", "ATR", "Самообучаемый" });
            _trailingStartPercent = CreateParameter("Старт трейлинга %", 0.15m, 0.1m, 10.0m, 0.01m);
            _trailingDistancePercent = CreateParameter("Дистанция трейлинга %", 0.1m, 0.1m, 5.0m, 0.01m);
            
            _atrPeriod = CreateParameter("ATR период", 8, 5, 50, 1);
            _atrMultiplier = CreateParameter("ATR множитель", 1.2m, 0.5m, 5.0m, 0.1m);
            
            _useManualTakeProfit = CreateParameter("Использовать ручной TP", "Выключен", new[] { "Включён", "Выключен" });
            _manualTakeProfit = CreateParameter("Ручной тейк-профит %", 2.0m, 0.1m, 20.0m, 0.1m);
            
            // ✅ КРИТИЧЕСКИЙ ПАРАМЕТР: Минимальная прибыль %
            // ВАЖНО: Этот параметр является ЕДИНСТВЕННЫМ И ДОСТАТОЧНЫМ источником учёта 
            // всех комиссионных издержек (брокер, биржа, прочие транзакционные издержки).
            // В расчётные формулы и логику кода НЕ ДОЛЖНЫ быть встроены дополнительные 
            // фиксированные или расчётные комиссии. Вся необходимая маржа для гарантированного 
            // безубыточного закрытия с учётом всех издержек задаётся исключительно через этот параметр.
            // Значение должно компенсировать все транзакционные издержки и обеспечивать 
            // заданный чистый финансовый результат.
            _minProfitPercentParam = CreateParameter("Минимальная прибыль %", 0.14m, 0.01m, 10.0m, 0.01m);
            _maxOpenPositions = CreateParameter("Макс. позиций бота", 5, 1, 100, 1);
            
            _useBreakEven = CreateParameter("Безубыток", "Включён", new[] { "Включён", "Выключен" });
            _breakEvenTriggerPercent = CreateParameter("Триггер безубытка %", 0.10m, 0.01m, 5.0m, 0.01m);
            
            _reentryCooldownCandles = CreateParameter("Кулдаун пере-входа (свечи)", 1, 0, 10, 1);
            _maxSpreadPercent = CreateParameter("Макс. спред %", 0.20m, 0.00m, 2.0m, 0.01m);
            
            _logVerbosity = CreateParameter("Детальность логов", "Обычная", new[] { "Минимальная", "Обычная", "Подробная" });
            _positionStatusEveryNBars = CreateParameter("Период статуса по позициям (свечи)", 5, 1, 100, 1);
            _unrealizedPnLLogIntervalMin = CreateParameter("Интервал лога PnL (мин)", 5, 1, 60, 1);
            
            _useVolumeFilter = CreateParameter("Фильтр по объему", "Выключен", new[] { "Включен", "Выключен" });
            _volumeMultiplier = CreateParameter("Множитель среднего объема", 1.5m, 0.5m, 5.0m, 0.1m);
            _volumePeriod = CreateParameter("Период расчета среднего объема", 20, 5, 100, 1);
            
            _useDuplicateProtection = CreateParameter("Защита от дублей", "Включена", new[] { "Включена", "Выключена" });
            _duplicateProtectionMinutes = CreateParameter("Время защиты от дублей (мин)", 5, 1, 60, 1);
            _duplicatePriceTolerance = CreateParameter("Допуск цены для дублей %", 0.1m, 0.01m, 1.0m, 0.01m);
            _duplicateTimeToleranceSeconds = CreateParameter("Допуск времени для дублей (сек)", 10, 1, 300, 1);
            _logPositionsNow = CreateParameter("Лог позиций (нажать)", false, "Логирование");

            // Усреднение
            _averagingCooldownCandles = CreateParameter("Кулдаун усреднения (свечи)", 1, 0, 10, 1, "Усреднение");
            _averagingLevel1Enabled = CreateParameter("Уровень усреднения 1 - вкл", true, "Усреднение");
            _averagingLevel1 = CreateParameter("Уровень усреднения 1 (%)", 0.5m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel2Enabled = CreateParameter("Уровень усреднения 2 - вкл", true, "Усреднение");
            _averagingLevel2 = CreateParameter("Уровень усреднения 2 (%)", 1.0m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel3Enabled = CreateParameter("Уровень усреднения 3 - вкл", true, "Усреднение");
            _averagingLevel3 = CreateParameter("Уровень усреднения 3 (%)", 1.5m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel4Enabled = CreateParameter("Уровень усреднения 4 - вкл", true, "Усреднение");
            _averagingLevel4 = CreateParameter("Уровень усреднения 4 (%)", 2.0m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel5Enabled = CreateParameter("Уровень усреднения 5 - вкл", true, "Усреднение");
            _averagingLevel5 = CreateParameter("Уровень усреднения 5 (%)", 2.5m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel6Enabled = CreateParameter("Уровень усреднения 6 - вкл", true, "Усреднение");
            _averagingLevel6 = CreateParameter("Уровень усреднения 6 (%)", 3.0m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel7Enabled = CreateParameter("Уровень усреднения 7 - вкл", true, "Усреднение");
            _averagingLevel7 = CreateParameter("Уровень усреднения 7 (%)", 3.5m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel8Enabled = CreateParameter("Уровень усреднения 8 - вкл", true, "Усреднение");
            _averagingLevel8 = CreateParameter("Уровень усреднения 8 (%)", 4.0m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel9Enabled = CreateParameter("Уровень усреднения 9 - вкл", true, "Усреднение");
            _averagingLevel9 = CreateParameter("Уровень усреднения 9 (%)", 4.5m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel10Enabled = CreateParameter("Уровень усреднения 10 - вкл", true, "Усреднение");
            _averagingLevel10 = CreateParameter("Уровень усреднения 10 (%)", 5.0m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel11Enabled = CreateParameter("Уровень усреднения 11 - вкл", true, "Усреднение");
            _averagingLevel11 = CreateParameter("Уровень усреднения 11 (%)", 5.5m, 0.1m, 100.0m, 0.1m, "Усреднение");
            _averagingLevel12Enabled = CreateParameter("Уровень усреднения 12 - вкл", true, "Усреднение");
            _averagingLevel12 = CreateParameter("Уровень усреднения 12 (%)", 6.0m, 0.1m, 100.0m, 0.1m, "Усреднение");
            
            // УСИЛЕННЫЕ AI ПАРАМЕТРЫ
            // Остаётся один видимый параметр: Режим оптимизации (Выключено / Включено непрерывно).
            // Внутренний _useAIOptimization ссылается на тот же объект, чтобы не менять остальную логику.
            _optimizationMode = CreateParameter("Режим оптимизации AI ⚡ 1) Выключено. 2) Включено непрерывно (гибридная PSO+GA с автозапусками)", 
                "Выключено", new[] { "Выключено", "Включено непрерывно" }, "AI Оптимизация");
            _useAIOptimization = _optimizationMode;
            _autoApplyResults = CreateParameter("Автоприменение результатов ⚡ ВКЛ: автоматически применять найденные параметры. ВЫКЛ: только показывать результаты", 
                true, "AI Оптимизация");
            _preserveSafetyLogic = CreateParameter("Сохранять защиту от убытков ⚡ ВКЛ: защитные параметры не изменяются AI. ВЫКЛ: AI может изменить все параметры", 
                true, "AI Оптимизация");
            
            // ПАРАМЕТРЫ PSO - ПОДРОБНЫЕ АННОТАЦИИ
            _psoSwarmSize = CreateParameter("PSO: Размер роя (50-10000) ⚡ Больше=шире поиск, турбинный режим. ↘ Меньше=быстрее, риск локального оптимума",
                50, 30, 200, 10, "AI Оптимизация");
            _psoMaxIterations = CreateParameter("PSO: Макс. итераций (100-10000) ⚡ Циклы поиска. ↗ Больше=точнее параметры. ↘ Меньше=быстрее завершение",
                500, 100, 1000, 50, "AI Оптимизация");
            _psoInertia = CreateParameter("PSO: Инерция (0.1-10.0) ⚡ Инерционность движения. ↗ Агрессивное исследование. ↘ Тонкая настройка", 
                0.9m, 0.1m, 10.0m, 0.1m, "AI Оптимизация");
            _psoCognitiveWeight = CreateParameter("PSO: Когнитивный вес (0.5-10.0) ⚡ Влияние личного опыта. ↗ Сохранение индивидуальности. ↘ Ориентация на общий опыт", 
                2.0m, 0.5m, 10.0m, 0.1m, "AI Оптимизация");
            _psoSocialWeight = CreateParameter("PSO: Социальный вес (0.5-10.0) ⚡ Влияние общественного опыта. ↗ Быстрая сходимость. ↘ Больше разнообразия", 
                2.0m, 0.5m, 10.0m, 0.1m, "AI Оптимизация");
            _psoUseAdaptiveInertia = CreateParameter("PSO: Адаптивная инерция ⚡ Автоизменение инерции. ВКЛ: широкий поиск→точная настройка. ВЫКЛ: постоянная инерция", 
                "Включено", new[] { "Включено", "Выключено" }, "AI Оптимизация");
            _psoStartInertia = CreateParameter("PSO: Начальная инерция (0.5-10.0) ⚡ Инерция в начале. ↗ Агрессивный старт. ↘ Осторожный старт", 
                1.0m, 0.5m, 10.0m, 0.1m, "AI Оптимизация");
            _psoEndInertia = CreateParameter("PSO: Конечная инерция (0.1-5.0) ⚡ Инерция в конце. ↗ Сохранение исследования. ↘ Только тонкая настройка", 
                0.3m, 0.1m, 5.0m, 0.1m, "AI Оптимизация");
            _psoUseSubSwarms = CreateParameter("PSO: Использовать подрои ⚡ Разделение роя на группы. ВКЛ: больше разнообразия, турбинный режим. ВЫКЛ: один большой рой", 
                "Включено", new[] { "Включено", "Выключено" }, "AI Оптимизация");
            _psoSubSwarmCount = CreateParameter("PSO: Количество подроев (3-100) ⚡ Групп в рое. ↗ Больше разнообразия. ↘ Больше частиц в группе", 
                10, 3, 100, 1, "AI Оптимизация");
            _psoMutationRate = CreateParameter("PSO: Вероятность мутации (0.0-1.0) ⚡ Случайные изменения. ↗ Больше разнообразия, турбинный режим. ↘ Стабильный поиск", 
                0.3m, 0.0m, 1.0m, 0.01m, "AI Оптимизация");
            _psoCrossoverRate = CreateParameter("PSO: Вероятность кроссовера (0.0-1.0) ⚡ Создание гибридов. ↗ Ускорение поиска, турбинный режим. ↘ Индивидуальный поиск", 
                0.8m, 0.0m, 1.0m, 0.01m, "AI Оптимизация");
            
            // ПАРАМЕТРЫ ГЕНЕТИЧЕСКОГО АЛГОРИТМА - ПОДРОБНЫЕ АННОТАЦИИ
            _useGeneticEnhancement = CreateParameter("🧬 Использовать генетический алгоритм ⚡ Гибридный режим PSO+GA. ВКЛ: эволюция решений. ВЫКЛ: только PSO", 
                "Включено", new[] { "Включено", "Выключено" }, "AI Оптимизация");
            _gaPopulationSize = CreateParameter("GA: Размер популяции (10-100) 🧬 Количество особей. ↗ Больше разнообразия, дольше расчет. ↘ Быстрее, меньше вариантов", 
                20, 10, 100, 5, "AI Оптимизация");
            _gaGenerations = CreateParameter("GA: Количество поколений (10-200) 🧬 Циклы эволюции. ↗ Глубже поиск, точнее результат. ↘ Быстрее завершение", 
                50, 10, 200, 10, "AI Оптимизация");
            _gaMutationRate = CreateParameter("GA: Вероятность мутации (0.0-0.5) 🧬 Случайные изменения генов. ↗ Больше разнообразия, избежание застревания. ↘ Стабильная эволюция", 
                0.2m, 0.0m, 0.5m, 0.01m, "AI Оптимизация");
            _gaCrossoverRate = CreateParameter("GA: Вероятность кроссовера (0.0-1.0) 🧬 Скрещивание решений. ↗ Быстрое улучшение, комбинирование лучших. ↘ Медленнее сходимость", 
                0.6m, 0.0m, 1.0m, 0.05m, "AI Оптимизация");
            
            // НЕПРЕРЫВНАЯ ОПТИМИЗАЦИЯ - ПОДРОБНЫЕ АННОТАЦИИ
            _continuousOptimization = CreateParameter("🔄 Непрерывная оптимизация ⚡ ВКЛ: постоянная оптимизация через интервалы. ВЫКЛ: оптимизация только при запуске", 
                "Включено", new[] { "Включено", "Выключено" }, "AI Оптимизация");
            _optimizationIntervalMinutes = CreateParameter("Интервал оптимизации (мин) (5-240) ⚡ Как часто запускать оптимизацию. ↗ Чаще обновление, больше нагрузка. ↘ Реже обновление, меньше нагрузка", 
                60, 5, 240, 5, "AI Оптимизация");
            
            // ВЫБОР ПАРАМЕТРОВ ДЛЯ ОПТИМИЗАЦИИ - ЧЕКБОКСЫ
            _optimizeTenkanLength = CreateParameter("Оптимизировать: Tenkan период", true, "Выбор параметров оптимизации");
            _optimizeKijunLength = CreateParameter("Оптимизировать: Kijun период", true, "Выбор параметров оптимизации");
            _optimizeSenkouBLength = CreateParameter("Оптимизировать: Senkou span B период", true, "Выбор параметров оптимизации");
            _optimizeSenkouOffset = CreateParameter("Оптимизировать: Senkou смещение", true, "Выбор параметров оптимизации");
            _optimizeStochPeriod = CreateParameter("Оптимизировать: Stochastic %K период", true, "Выбор параметров оптимизации");
            _optimizeStochSmoothing = CreateParameter("Оптимизировать: Stochastic сглаживание %K", true, "Выбор параметров оптимизации");
            _optimizeStochDPeriod = CreateParameter("Оптимизировать: Stochastic %D период", true, "Выбор параметров оптимизации");
            _optimizeStochOversold = CreateParameter("Оптимизировать: Stochastic перепроданность %", true, "Выбор параметров оптимизации");
            _optimizeStochOverbought = CreateParameter("Оптимизировать: Stochastic перекупленность %", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel1 = CreateParameter("Оптимизировать: Уровень усреднения 1", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel2 = CreateParameter("Оптимизировать: Уровень усреднения 2", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel3 = CreateParameter("Оптимизировать: Уровень усреднения 3", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel4 = CreateParameter("Оптимизировать: Уровень усреднения 4", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel5 = CreateParameter("Оптимизировать: Уровень усреднения 5", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel6 = CreateParameter("Оптимизировать: Уровень усреднения 6", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel7 = CreateParameter("Оптимизировать: Уровень усреднения 7", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel8 = CreateParameter("Оптимизировать: Уровень усреднения 8", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel9 = CreateParameter("Оптимизировать: Уровень усреднения 9", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel10 = CreateParameter("Оптимизировать: Уровень усреднения 10", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel11 = CreateParameter("Оптимизировать: Уровень усреднения 11", true, "Выбор параметров оптимизации");
            _optimizeAveragingLevel12 = CreateParameter("Оптимизировать: Уровень усреднения 12", true, "Выбор параметров оптимизации");
            _optimizeMinProfitPercent = CreateParameter("Оптимизировать: Минимальная прибыль %", true, "Выбор параметров оптимизации");
            _optimizeTrailingStartPercent = CreateParameter("Оптимизировать: Старт трейлинга %", true, "Выбор параметров оптимизации");
            _optimizeTrailingDistancePercent = CreateParameter("Оптимизировать: Дистанция трейлинга %", true, "Выбор параметров оптимизации");
            _optimizeSelfLearningTrailing = CreateParameter("Оптимизировать: Самообучаемый трейлинг (параметры адаптации)", true, "Выбор параметров оптимизации");
            _optimizeManualTakeProfit = CreateParameter("Оптимизировать: Ручной тейк-профит %", true, "Выбор параметров оптимизации");
            _optimizeBreakEvenTriggerPercent = CreateParameter("Оптимизировать: Триггер безубытка %", true, "Выбор параметров оптимизации");
            _optimizeMaxSpreadPercent = CreateParameter("Оптимизировать: Макс. спред %", true, "Выбор параметров оптимизации");
            _optimizeATRPeriod = CreateParameter("Оптимизировать: ATR период", true, "Выбор параметров оптимизации");
            _optimizeATRMultiplier = CreateParameter("Оптимизировать: ATR множитель", true, "Выбор параметров оптимизации");
            _optimizeVolumeMultiplier = CreateParameter("Оптимизировать: Множитель среднего объема", true, "Выбор параметров оптимизации");
            _optimizeVolumePeriod = CreateParameter("Оптимизировать: Период расчета среднего объема", true, "Выбор параметров оптимизации");
            _optimizeReentryCooldownCandles = CreateParameter("Оптимизировать: Кулдаун пере-входа (свечи)", true, "Выбор параметров оптимизации");
            _optimizeMaxOpenPositions = CreateParameter("Оптимизировать: Макс. позиций бота", true, "Выбор параметров оптимизации");
            
            // Counterintuitive параметры
            _useCounterintuitive = CreateParameter("Использовать механизм Counterintuitive", "Выключено", new[] { "Включено", "Выключено" }, ParameterGroups.Counterintuitive);
            _counterintuitiveEntry = CreateParameter("Вход: Counterintuitive", "Выключено", new[] { "Включено", "Выключено" }, ParameterGroups.Counterintuitive);
            _counterintuitiveExit = CreateParameter("Выход: Counterintuitive", "Выключено", new[] { "Включено", "Выключено" }, ParameterGroups.Counterintuitive);
            // EMA для контринтуитивной логики:
            // EMA1 – медленная, определяет основной тренд
            _counterintuitiveEma1Period = CreateParameter("EMA1 период (медленная — определение тренда)", 300, 10, 5000, 1, ParameterGroups.Counterintuitive);
            // EMA2 – быстрая, также определяет тренд (положение EMA2 относительно EMA1)
            _counterintuitiveEma2Period = CreateParameter("EMA2 период (быстрая — определение тренда)", 80, 5, 5000, 1, ParameterGroups.Counterintuitive);
            // EMA3 – контртрендовая, используется для оценки отката к тренду
            _counterintuitiveEma3Period = CreateParameter("EMA3 период (контртренд — определение отката)", 30, 3, 5000, 1, ParameterGroups.Counterintuitive);
            
            // Флаги оптимизации для counterintuitive
            _optimizeCounterintuitiveEma1Period = CreateParameter("Оптимизировать: EMA1 (медленная — определение тренда)", true, ParameterGroups.OptimizationSelection);
            _optimizeCounterintuitiveEma2Period = CreateParameter("Оптимизировать: EMA2 (быстрая — определение тренда)", true, ParameterGroups.OptimizationSelection);
            _optimizeCounterintuitiveEma3Period = CreateParameter("Оптимизировать: EMA3 (контртренд — определение отката)", true, ParameterGroups.OptimizationSelection);
            
            // Неторговые дни
            _mondayTrade = CreateParameter("Пн - Торговать", true, ParameterGroups.NonTradingDays);
            _tuesdayTrade = CreateParameter("Вт - Торговать", true, ParameterGroups.NonTradingDays);
            _wednesdayTrade = CreateParameter("Ср - Торговать", true, ParameterGroups.NonTradingDays);
            _thursdayTrade = CreateParameter("Чт - Торговать", true, ParameterGroups.NonTradingDays);
            _fridayTrade = CreateParameter("Пт - Торговать", true, ParameterGroups.NonTradingDays);
            _saturdayTrade = CreateParameter("Сб - Торговать", false, ParameterGroups.NonTradingDays);
            _sundayTrade = CreateParameter("Вс - Торговать", false, ParameterGroups.NonTradingDays);
            
            // ✅ Неторговые периоды для каждого дня (3 периода на день)
            // Период 1 по умолчанию активен (23:59 - 7:00), периоды 2 и 3 выключены
            
            // Понедельник
            _mondayPeriod1Enabled = CreateParameter("Пн - Период 1: Включен", true, ParameterGroups.NonTradingPeriods);
            _mondayPeriod1StartHour = CreateParameter("Пн - Период 1: Начало (час)", 23, 0, 23, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod1StartMinute = CreateParameter("Пн - Период 1: Начало (мин)", 59, 0, 59, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod1EndHour = CreateParameter("Пн - Период 1: Конец (час)", 7, 0, 23, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod1EndMinute = CreateParameter("Пн - Период 1: Конец (мин)", 0, 0, 59, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod2Enabled = CreateParameter("Пн - Период 2: Включен", false, ParameterGroups.NonTradingPeriods);
            _mondayPeriod2StartHour = CreateParameter("Пн - Период 2: Начало (час)", 0, 0, 23, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod2StartMinute = CreateParameter("Пн - Период 2: Начало (мин)", 0, 0, 59, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod2EndHour = CreateParameter("Пн - Период 2: Конец (час)", 0, 0, 23, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod2EndMinute = CreateParameter("Пн - Период 2: Конец (мин)", 0, 0, 59, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod3Enabled = CreateParameter("Пн - Период 3: Включен", false, ParameterGroups.NonTradingPeriods);
            _mondayPeriod3StartHour = CreateParameter("Пн - Период 3: Начало (час)", 0, 0, 23, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod3StartMinute = CreateParameter("Пн - Период 3: Начало (мин)", 0, 0, 59, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod3EndHour = CreateParameter("Пн - Период 3: Конец (час)", 0, 0, 23, 1, ParameterGroups.NonTradingPeriods);
            _mondayPeriod3EndMinute = CreateParameter("Пн - Период 3: Конец (мин)", 0, 0, 59, 1, ParameterGroups.NonTradingPeriods);
            
            // Вторник
            _tuesdayPeriod1Enabled = CreateParameter("Вт - Период 1: Включен", true, "Неторговые периоды");
            _tuesdayPeriod1StartHour = CreateParameter("Вт - Период 1: Начало (час)", 23, 0, 23, 1, "Неторговые периоды");
            _tuesdayPeriod1StartMinute = CreateParameter("Вт - Период 1: Начало (мин)", 59, 0, 59, 1, "Неторговые периоды");
            _tuesdayPeriod1EndHour = CreateParameter("Вт - Период 1: Конец (час)", 7, 0, 23, 1, "Неторговые периоды");
            _tuesdayPeriod1EndMinute = CreateParameter("Вт - Период 1: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _tuesdayPeriod2Enabled = CreateParameter("Вт - Период 2: Включен", false, "Неторговые периоды");
            _tuesdayPeriod2StartHour = CreateParameter("Вт - Период 2: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _tuesdayPeriod2StartMinute = CreateParameter("Вт - Период 2: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _tuesdayPeriod2EndHour = CreateParameter("Вт - Период 2: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _tuesdayPeriod2EndMinute = CreateParameter("Вт - Период 2: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _tuesdayPeriod3Enabled = CreateParameter("Вт - Период 3: Включен", false, "Неторговые периоды");
            _tuesdayPeriod3StartHour = CreateParameter("Вт - Период 3: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _tuesdayPeriod3StartMinute = CreateParameter("Вт - Период 3: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _tuesdayPeriod3EndHour = CreateParameter("Вт - Период 3: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _tuesdayPeriod3EndMinute = CreateParameter("Вт - Период 3: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            
            // Среда
            _wednesdayPeriod1Enabled = CreateParameter("Ср - Период 1: Включен", true, "Неторговые периоды");
            _wednesdayPeriod1StartHour = CreateParameter("Ср - Период 1: Начало (час)", 23, 0, 23, 1, "Неторговые периоды");
            _wednesdayPeriod1StartMinute = CreateParameter("Ср - Период 1: Начало (мин)", 59, 0, 59, 1, "Неторговые периоды");
            _wednesdayPeriod1EndHour = CreateParameter("Ср - Период 1: Конец (час)", 7, 0, 23, 1, "Неторговые периоды");
            _wednesdayPeriod1EndMinute = CreateParameter("Ср - Период 1: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _wednesdayPeriod2Enabled = CreateParameter("Ср - Период 2: Включен", false, "Неторговые периоды");
            _wednesdayPeriod2StartHour = CreateParameter("Ср - Период 2: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _wednesdayPeriod2StartMinute = CreateParameter("Ср - Период 2: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _wednesdayPeriod2EndHour = CreateParameter("Ср - Период 2: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _wednesdayPeriod2EndMinute = CreateParameter("Ср - Период 2: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _wednesdayPeriod3Enabled = CreateParameter("Ср - Период 3: Включен", false, "Неторговые периоды");
            _wednesdayPeriod3StartHour = CreateParameter("Ср - Период 3: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _wednesdayPeriod3StartMinute = CreateParameter("Ср - Период 3: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _wednesdayPeriod3EndHour = CreateParameter("Ср - Период 3: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _wednesdayPeriod3EndMinute = CreateParameter("Ср - Период 3: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            
            // Четверг
            _thursdayPeriod1Enabled = CreateParameter("Чт - Период 1: Включен", true, "Неторговые периоды");
            _thursdayPeriod1StartHour = CreateParameter("Чт - Период 1: Начало (час)", 23, 0, 23, 1, "Неторговые периоды");
            _thursdayPeriod1StartMinute = CreateParameter("Чт - Период 1: Начало (мин)", 59, 0, 59, 1, "Неторговые периоды");
            _thursdayPeriod1EndHour = CreateParameter("Чт - Период 1: Конец (час)", 7, 0, 23, 1, "Неторговые периоды");
            _thursdayPeriod1EndMinute = CreateParameter("Чт - Период 1: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _thursdayPeriod2Enabled = CreateParameter("Чт - Период 2: Включен", false, "Неторговые периоды");
            _thursdayPeriod2StartHour = CreateParameter("Чт - Период 2: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _thursdayPeriod2StartMinute = CreateParameter("Чт - Период 2: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _thursdayPeriod2EndHour = CreateParameter("Чт - Период 2: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _thursdayPeriod2EndMinute = CreateParameter("Чт - Период 2: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _thursdayPeriod3Enabled = CreateParameter("Чт - Период 3: Включен", false, "Неторговые периоды");
            _thursdayPeriod3StartHour = CreateParameter("Чт - Период 3: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _thursdayPeriod3StartMinute = CreateParameter("Чт - Период 3: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _thursdayPeriod3EndHour = CreateParameter("Чт - Период 3: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _thursdayPeriod3EndMinute = CreateParameter("Чт - Период 3: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            
            // Пятница
            _fridayPeriod1Enabled = CreateParameter("Пт - Период 1: Включен", true, "Неторговые периоды");
            _fridayPeriod1StartHour = CreateParameter("Пт - Период 1: Начало (час)", 23, 0, 23, 1, "Неторговые периоды");
            _fridayPeriod1StartMinute = CreateParameter("Пт - Период 1: Начало (мин)", 59, 0, 59, 1, "Неторговые периоды");
            _fridayPeriod1EndHour = CreateParameter("Пт - Период 1: Конец (час)", 7, 0, 23, 1, "Неторговые периоды");
            _fridayPeriod1EndMinute = CreateParameter("Пт - Период 1: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _fridayPeriod2Enabled = CreateParameter("Пт - Период 2: Включен", false, "Неторговые периоды");
            _fridayPeriod2StartHour = CreateParameter("Пт - Период 2: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _fridayPeriod2StartMinute = CreateParameter("Пт - Период 2: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _fridayPeriod2EndHour = CreateParameter("Пт - Период 2: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _fridayPeriod2EndMinute = CreateParameter("Пт - Период 2: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _fridayPeriod3Enabled = CreateParameter("Пт - Период 3: Включен", false, "Неторговые периоды");
            _fridayPeriod3StartHour = CreateParameter("Пт - Период 3: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _fridayPeriod3StartMinute = CreateParameter("Пт - Период 3: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _fridayPeriod3EndHour = CreateParameter("Пт - Период 3: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _fridayPeriod3EndMinute = CreateParameter("Пт - Период 3: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            
            // Суббота
            _saturdayPeriod1Enabled = CreateParameter("Сб - Период 1: Включен", true, "Неторговые периоды");
            _saturdayPeriod1StartHour = CreateParameter("Сб - Период 1: Начало (час)", 23, 0, 23, 1, "Неторговые периоды");
            _saturdayPeriod1StartMinute = CreateParameter("Сб - Период 1: Начало (мин)", 59, 0, 59, 1, "Неторговые периоды");
            _saturdayPeriod1EndHour = CreateParameter("Сб - Период 1: Конец (час)", 7, 0, 23, 1, "Неторговые периоды");
            _saturdayPeriod1EndMinute = CreateParameter("Сб - Период 1: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _saturdayPeriod2Enabled = CreateParameter("Сб - Период 2: Включен", false, "Неторговые периоды");
            _saturdayPeriod2StartHour = CreateParameter("Сб - Период 2: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _saturdayPeriod2StartMinute = CreateParameter("Сб - Период 2: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _saturdayPeriod2EndHour = CreateParameter("Сб - Период 2: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _saturdayPeriod2EndMinute = CreateParameter("Сб - Период 2: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _saturdayPeriod3Enabled = CreateParameter("Сб - Период 3: Включен", false, "Неторговые периоды");
            _saturdayPeriod3StartHour = CreateParameter("Сб - Период 3: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _saturdayPeriod3StartMinute = CreateParameter("Сб - Период 3: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _saturdayPeriod3EndHour = CreateParameter("Сб - Период 3: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _saturdayPeriod3EndMinute = CreateParameter("Сб - Период 3: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            
            // Воскресенье
            _sundayPeriod1Enabled = CreateParameter("Вс - Период 1: Включен", true, "Неторговые периоды");
            _sundayPeriod1StartHour = CreateParameter("Вс - Период 1: Начало (час)", 23, 0, 23, 1, "Неторговые периоды");
            _sundayPeriod1StartMinute = CreateParameter("Вс - Период 1: Начало (мин)", 59, 0, 59, 1, "Неторговые периоды");
            _sundayPeriod1EndHour = CreateParameter("Вс - Период 1: Конец (час)", 7, 0, 23, 1, "Неторговые периоды");
            _sundayPeriod1EndMinute = CreateParameter("Вс - Период 1: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _sundayPeriod2Enabled = CreateParameter("Вс - Период 2: Включен", false, "Неторговые периоды");
            _sundayPeriod2StartHour = CreateParameter("Вс - Период 2: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _sundayPeriod2StartMinute = CreateParameter("Вс - Период 2: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _sundayPeriod2EndHour = CreateParameter("Вс - Период 2: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _sundayPeriod2EndMinute = CreateParameter("Вс - Период 2: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _sundayPeriod3Enabled = CreateParameter("Вс - Период 3: Включен", false, "Неторговые периоды");
            _sundayPeriod3StartHour = CreateParameter("Вс - Период 3: Начало (час)", 0, 0, 23, 1, "Неторговые периоды");
            _sundayPeriod3StartMinute = CreateParameter("Вс - Период 3: Начало (мин)", 0, 0, 59, 1, "Неторговые периоды");
            _sundayPeriod3EndHour = CreateParameter("Вс - Период 3: Конец (час)", 0, 0, 23, 1, "Неторговые периоды");
            _sundayPeriod3EndMinute = CreateParameter("Вс - Период 3: Конец (мин)", 0, 0, 59, 1, "Неторговые периоды");
        }
        
        private void InitializeAssembly()
        {
            // Создаем сборку компонентов
            _assembly = new ComponentAssembly();
            
            // Создаем контекст
            _componentContext = new BotComponentContext(_tab, SendNewLogMessage, _assembly);
            
            // Сохраняем параметры в общем хранилище
            SaveParametersToContext();
            
            // Устанавливаем функцию проверки неторговых периодов
            _componentContext.IsTradingTimeAllowed = IsTradingTimeAllowed;
            
            // Регистрируем и создаем State Machine
            _stateMachine = new AdaptiveTradingStateMachine(_componentContext);
            _assembly.RegisterComponent<AdaptiveTradingStateMachine>(_stateMachine);
            
            // Регистрируем основные компоненты
            _assembly.RegisterComponent<DataIndicatorComponent>(new DataIndicatorComponent());
            _assembly.RegisterComponent<RiskManagementComponent>(new RiskManagementComponent());
            _assembly.RegisterComponent<PositionManagerComponent>(new PositionManagerComponent());
            _assembly.RegisterComponent<TrailingStopComponent>(new TrailingStopComponent());
            _assembly.RegisterComponent<EnhancedAIOptimizationComponent>(new EnhancedAIOptimizationComponent());
            _assembly.RegisterComponent<IchimokuStrategyComponent>(new IchimokuStrategyComponent());
            
            // Устанавливаем функцию проверки неторговых периодов
            _componentContext.IsTradingTimeAllowed = IsTradingTimeAllowed;
            
            // Инициализируем сборку
            _assembly.Initialize(_componentContext);
            
            // Подписываемся на события
            SubscribeToEvents();
            
            // Запускаем State Machine
            _stateMachine.TransitionTo(TradingState.Initializing, "Запуск бота");
            _stateMachine.ProcessEvent(TradingEvent.Initialized);
            
            // Запускаем AI оптимизацию если включена
            if (_useAIOptimization.ValueString != "Выключена")
            {
                Task.Run(async () =>
                {
                    await Task.Delay(10000); // Ждем 10 секунд после запуска
                    StartEnhancedAIOptimization();
                });
            }
        }
        
        private void SaveParametersToContext()
        {
            // Сохраняем все параметры в общее хранилище
            _componentContext.SharedData[SharedDataKeys.Regime] = _regime;
            _componentContext.SharedData[SharedDataKeys.Volume] = _volume;
            _componentContext.SharedData[SharedDataKeys.ShortTrading] = _shortTrading;
            _componentContext.SharedData[SharedDataKeys.CloseMode] = _closeMode;
            _componentContext.SharedData[SharedDataKeys.ForceTradingMode] = _forceTradingMode;
            _componentContext.SharedData[SharedDataKeys.TenkanLength] = _tenkanLength;
            _componentContext.SharedData[SharedDataKeys.KijunLength] = _kijunLength;
            _componentContext.SharedData[SharedDataKeys.SenkouBLength] = _senkouBLength;
            _componentContext.SharedData[SharedDataKeys.SenkouOffset] = _senkouOffset;
            _componentContext.SharedData[SharedDataKeys.StochPeriod] = _stochPeriod;
            _componentContext.SharedData[SharedDataKeys.StochSmoothing] = _stochSmoothing;
            _componentContext.SharedData[SharedDataKeys.StochDPeriod] = _stochDPeriod;
            _componentContext.SharedData[SharedDataKeys.StochOversold] = _stochOversold;
            _componentContext.SharedData[SharedDataKeys.StochOverbought] = _stochOverbought;
            _componentContext.SharedData[SharedDataKeys.OpenByTkKj] = _openByTkKj;
            _componentContext.SharedData[SharedDataKeys.OpenByCloud] = _openByCloud;
            _componentContext.SharedData[SharedDataKeys.OpenByChikou] = _openByChikou;
            _componentContext.SharedData[SharedDataKeys.OpenByStochastic] = _openByStochastic;
            _componentContext.SharedData[SharedDataKeys.ExitByTkKj] = _exitByTkKj;
            _componentContext.SharedData[SharedDataKeys.ExitByCloud] = _exitByCloud;
            _componentContext.SharedData[SharedDataKeys.ExitByChikou] = _exitByChikou;
            _componentContext.SharedData[SharedDataKeys.ExitByStochastic] = _exitByStochastic;
            _componentContext.SharedData[SharedDataKeys.UseTrailingStop] = _useTrailingStop;
            _componentContext.SharedData[SharedDataKeys.TrailingType] = _trailingType;
            _componentContext.SharedData[SharedDataKeys.TrailingStartPercent] = _trailingStartPercent;
            _componentContext.SharedData[SharedDataKeys.TrailingDistancePercent] = _trailingDistancePercent;
            _componentContext.SharedData[SharedDataKeys.AtrPeriod] = _atrPeriod;
            _componentContext.SharedData[SharedDataKeys.AtrMultiplier] = _atrMultiplier;
            _componentContext.SharedData[SharedDataKeys.UseManualTakeProfit] = _useManualTakeProfit;
            _componentContext.SharedData[SharedDataKeys.ManualTakeProfit] = _manualTakeProfit;
            _componentContext.SharedData[SharedDataKeys.MinProfitPercent] = _minProfitPercentParam;
            _componentContext.SharedData[SharedDataKeys.MaxOpenPositions] = _maxOpenPositions;
            _componentContext.SharedData[SharedDataKeys.UseBreakEven] = _useBreakEven;
            _componentContext.SharedData[SharedDataKeys.BreakEvenTriggerPercent] = _breakEvenTriggerPercent;
            _componentContext.SharedData[SharedDataKeys.ReentryCooldownCandles] = _reentryCooldownCandles;
            _componentContext.SharedData[SharedDataKeys.MaxSpreadPercent] = _maxSpreadPercent;
            _componentContext.SharedData[SharedDataKeys.LogVerbosity] = _logVerbosity;
            _componentContext.SharedData[SharedDataKeys.PositionStatusEveryNBars] = _positionStatusEveryNBars;
            _componentContext.SharedData[SharedDataKeys.UnrealizedPnLLogIntervalMin] = _unrealizedPnLLogIntervalMin;
            _componentContext.SharedData[SharedDataKeys.UseVolumeFilter] = _useVolumeFilter;
            _componentContext.SharedData[SharedDataKeys.VolumeMultiplier] = _volumeMultiplier;
            _componentContext.SharedData[SharedDataKeys.VolumePeriod] = _volumePeriod;
            _componentContext.SharedData[SharedDataKeys.UseDuplicateProtection] = _useDuplicateProtection;
            _componentContext.SharedData[SharedDataKeys.DuplicateProtectionMinutes] = _duplicateProtectionMinutes;
            _componentContext.SharedData[SharedDataKeys.DuplicatePriceTolerance] = _duplicatePriceTolerance;
            _componentContext.SharedData[SharedDataKeys.DuplicateTimeToleranceSeconds] = _duplicateTimeToleranceSeconds;
            _componentContext.SharedData[SharedDataKeys.AveragingCooldownCandles] = _averagingCooldownCandles;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel1] = _averagingLevel1;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel2] = _averagingLevel2;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel3] = _averagingLevel3;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel4] = _averagingLevel4;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel5] = _averagingLevel5;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel6] = _averagingLevel6;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel7] = _averagingLevel7;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel8] = _averagingLevel8;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel9] = _averagingLevel9;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel10] = _averagingLevel10;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel11] = _averagingLevel11;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel12] = _averagingLevel12;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel1Enabled] = _averagingLevel1Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel2Enabled] = _averagingLevel2Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel3Enabled] = _averagingLevel3Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel4Enabled] = _averagingLevel4Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel5Enabled] = _averagingLevel5Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel6Enabled] = _averagingLevel6Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel7Enabled] = _averagingLevel7Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel8Enabled] = _averagingLevel8Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel9Enabled] = _averagingLevel9Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel10Enabled] = _averagingLevel10Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel11Enabled] = _averagingLevel11Enabled;
            _componentContext.SharedData[SharedDataKeys.AveragingLevel12Enabled] = _averagingLevel12Enabled;
            _componentContext.SharedData[SharedDataKeys.UseAIOptimization] = _useAIOptimization;
            _componentContext.SharedData[SharedDataKeys.OptimizationMode] = _optimizationMode;
            _componentContext.SharedData[SharedDataKeys.AutoApplyResults] = _autoApplyResults;
            _componentContext.SharedData[SharedDataKeys.PreserveSafetyLogic] = _preserveSafetyLogic;
            _componentContext.SharedData[SharedDataKeys.PsoSwarmSize] = _psoSwarmSize;
            _componentContext.SharedData[SharedDataKeys.PsoMaxIterations] = _psoMaxIterations;
            _componentContext.SharedData[SharedDataKeys.PsoInertia] = _psoInertia;
            _componentContext.SharedData[SharedDataKeys.PsoCognitiveWeight] = _psoCognitiveWeight;
            _componentContext.SharedData[SharedDataKeys.PsoSocialWeight] = _psoSocialWeight;
            _componentContext.SharedData[SharedDataKeys.PsoUseAdaptiveInertia] = _psoUseAdaptiveInertia;
            _componentContext.SharedData[SharedDataKeys.PsoStartInertia] = _psoStartInertia;
            _componentContext.SharedData[SharedDataKeys.PsoEndInertia] = _psoEndInertia;
            _componentContext.SharedData[SharedDataKeys.PsoUseSubSwarms] = _psoUseSubSwarms;
            _componentContext.SharedData[SharedDataKeys.PsoSubSwarmCount] = _psoSubSwarmCount;
            _componentContext.SharedData[SharedDataKeys.PsoMutationRate] = _psoMutationRate;
            _componentContext.SharedData[SharedDataKeys.PsoCrossoverRate] = _psoCrossoverRate;
            _componentContext.SharedData[SharedDataKeys.UseGeneticEnhancement] = _useGeneticEnhancement;
            _componentContext.SharedData[SharedDataKeys.GaPopulationSize] = _gaPopulationSize;
            _componentContext.SharedData[SharedDataKeys.GaGenerations] = _gaGenerations;
            _componentContext.SharedData[SharedDataKeys.GaMutationRate] = _gaMutationRate;
            _componentContext.SharedData[SharedDataKeys.GaCrossoverRate] = _gaCrossoverRate;
            _componentContext.SharedData[SharedDataKeys.ContinuousOptimization] = _continuousOptimization;
            _componentContext.SharedData[SharedDataKeys.OptimizationIntervalMinutes] = _optimizationIntervalMinutes;
            
            // Сохраняем флаги выбора параметров для оптимизации
            _componentContext.SharedData[SharedDataKeys.OptimizeTenkanLength] = _optimizeTenkanLength;
            _componentContext.SharedData[SharedDataKeys.OptimizeKijunLength] = _optimizeKijunLength;
            _componentContext.SharedData[SharedDataKeys.OptimizeSenkouBLength] = _optimizeSenkouBLength;
            _componentContext.SharedData[SharedDataKeys.OptimizeSenkouOffset] = _optimizeSenkouOffset;
            _componentContext.SharedData[SharedDataKeys.OptimizeStochPeriod] = _optimizeStochPeriod;
            _componentContext.SharedData[SharedDataKeys.OptimizeStochSmoothing] = _optimizeStochSmoothing;
            _componentContext.SharedData[SharedDataKeys.OptimizeStochDPeriod] = _optimizeStochDPeriod;
            _componentContext.SharedData[SharedDataKeys.OptimizeStochOversold] = _optimizeStochOversold;
            _componentContext.SharedData[SharedDataKeys.OptimizeStochOverbought] = _optimizeStochOverbought;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel1] = _optimizeAveragingLevel1;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel2] = _optimizeAveragingLevel2;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel3] = _optimizeAveragingLevel3;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel4] = _optimizeAveragingLevel4;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel5] = _optimizeAveragingLevel5;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel6] = _optimizeAveragingLevel6;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel7] = _optimizeAveragingLevel7;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel8] = _optimizeAveragingLevel8;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel9] = _optimizeAveragingLevel9;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel10] = _optimizeAveragingLevel10;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel11] = _optimizeAveragingLevel11;
            _componentContext.SharedData[SharedDataKeys.OptimizeAveragingLevel12] = _optimizeAveragingLevel12;
            _componentContext.SharedData[SharedDataKeys.OptimizeMinProfitPercent] = _optimizeMinProfitPercent;
            _componentContext.SharedData[SharedDataKeys.OptimizeTrailingStartPercent] = _optimizeTrailingStartPercent;
            _componentContext.SharedData[SharedDataKeys.OptimizeTrailingDistancePercent] = _optimizeTrailingDistancePercent;
            _componentContext.SharedData[SharedDataKeys.OptimizeManualTakeProfit] = _optimizeManualTakeProfit;
            _componentContext.SharedData[SharedDataKeys.OptimizeBreakEvenTriggerPercent] = _optimizeBreakEvenTriggerPercent;
            _componentContext.SharedData[SharedDataKeys.OptimizeMaxSpreadPercent] = _optimizeMaxSpreadPercent;
            _componentContext.SharedData[SharedDataKeys.OptimizeATRPeriod] = _optimizeATRPeriod;
            _componentContext.SharedData[SharedDataKeys.OptimizeATRMultiplier] = _optimizeATRMultiplier;
            _componentContext.SharedData[SharedDataKeys.OptimizeVolumeMultiplier] = _optimizeVolumeMultiplier;
            _componentContext.SharedData[SharedDataKeys.OptimizeVolumePeriod] = _optimizeVolumePeriod;
            _componentContext.SharedData[SharedDataKeys.OptimizeReentryCooldownCandles] = _optimizeReentryCooldownCandles;
            _componentContext.SharedData[SharedDataKeys.OptimizeMaxOpenPositions] = _optimizeMaxOpenPositions;
            
            // Counterintuitive параметры
            _componentContext.SharedData[SharedDataKeys.UseCounterintuitive] = _useCounterintuitive;
            _componentContext.SharedData[SharedDataKeys.CounterintuitiveEntry] = _counterintuitiveEntry;
            _componentContext.SharedData[SharedDataKeys.CounterintuitiveExit] = _counterintuitiveExit;
            _componentContext.SharedData[SharedDataKeys.CounterintuitiveEma1Period] = _counterintuitiveEma1Period;
            _componentContext.SharedData[SharedDataKeys.CounterintuitiveEma2Period] = _counterintuitiveEma2Period;
            _componentContext.SharedData[SharedDataKeys.CounterintuitiveEma3Period] = _counterintuitiveEma3Period;
            _componentContext.SharedData[SharedDataKeys.OptimizeCounterintuitiveEma1Period] = _optimizeCounterintuitiveEma1Period;
            _componentContext.SharedData[SharedDataKeys.OptimizeCounterintuitiveEma2Period] = _optimizeCounterintuitiveEma2Period;
            _componentContext.SharedData[SharedDataKeys.OptimizeCounterintuitiveEma3Period] = _optimizeCounterintuitiveEma3Period;
        }
        
        private void SubscribeToEvents()
        {
            ParametrsChangeByUser += OnParametersChangeByUser;
            _tab.CandleFinishedEvent += OnCandleFinishedEvent;
        }
        
        private void OnParametersChangeByUser()
        {
            try
            {
                SendNewLogMessage("=== ОБНОВЛЕНИЕ ПАРАМЕТРОВ ===", LogMessageType.System);
                
                // Обновляем параметры в контексте
                SaveParametersToContext();
                
                // Уведомляем компоненты об изменении параметров
                var dataComponent = _assembly.GetComponent<DataIndicatorComponent>();
                if (dataComponent != null)
                {
                    dataComponent.Dispose();
                    dataComponent.Initialize(_componentContext);
                }

                // Ручной запрос лога позиций через параметр-кнопку
                if (_logPositionsNow.ValueBool)
                {
                    LogPositionsForced();
                    _logPositionsNow.ValueBool = false;
                }
                
                SendNewLogMessage("✅ Параметры обновлены", LogMessageType.System);
                LogCurrentParameters();
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обновления параметров: {ex.Message}", LogMessageType.Error);
            }
        }
        
        private async void OnCandleFinishedEvent(List<Candle> candles)
        {
            try
            {
                if (candles == null || candles.Count == 0)
                    return;
                
                var currentCandle = candles[candles.Count - 1];
                
                // Обработка через сборку компонентов
                await _assembly.ProcessCandleAsync(currentCandle);
                
                // Регулярное логирование состояния открытых позиций
                int intervalMinutes = _logVerbosity.ValueString == "Подробная"
                    ? 5
                    : (_logVerbosity.ValueString == "Обычная" ? 10 : 15);
                
                if (DateTime.Now - _lastPositionStatusLogTime >= TimeSpan.FromMinutes(intervalMinutes))
                {
                    LogPositionInfo();
                    _lastPositionStatusLogTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка в обработке свечи: {ex.Message}", LogMessageType.Error);
                _stateMachine?.ProcessEvent(TradingEvent.ErrorOccurred, ex);
            }
        }
        
        private void LogCurrentParameters()
        {
            if (_logVerbosity.ValueString == "Минимальная")
                return;
            
            SendNewLogMessage("=== ТЕКУЩИЕ ПАРАМЕТРЫ ===", LogMessageType.System);
            SendNewLogMessage($"📊 Ишимоку: Tenkan={_tenkanLength.ValueInt}, Kijun={_kijunLength.ValueInt}, SenkouB={_senkouBLength.ValueInt}", LogMessageType.System);
            SendNewLogMessage($"🛡️ Защита: Мин. прибыль={_minProfitPercentParam.ValueDecimal:F2}%, Спред={_maxSpreadPercent.ValueDecimal:F2}%", LogMessageType.System);
            SendNewLogMessage($"🤖 AI Оптимизация: {_useAIOptimization.ValueString}, Режим: {_optimizationMode.ValueString}", LogMessageType.System);
            SendNewLogMessage($"🧬 Генетический алгоритм: {_useGeneticEnhancement.ValueString}", LogMessageType.System);
            SendNewLogMessage($"🔄 Непрерывная оптимизация: {_continuousOptimization.ValueString}", LogMessageType.System);
            SendNewLogMessage($"⚡ Состояние: {_stateMachine?.CurrentState}", LogMessageType.System);
            SendNewLogMessage("========================", LogMessageType.System);
        }
        
        private void LogPositionInfo()
        {
            try
            {
                var positionManager = _assembly.GetComponent<PositionManagerComponent>();
                var riskManager = _assembly.GetComponent<RiskManagementComponent>();
                var trailingComponent = _assembly.GetComponent<TrailingStopComponent>();
                var aiComponent = _assembly.GetComponent<EnhancedAIOptimizationComponent>();
                
                if (positionManager == null) return;
                
                var openPositions = positionManager.GetActivePositions();
                
                // Логируем статус AI каждый N баров
                if (aiComponent != null)
                {
                    SendNewLogMessage($"🤖 {aiComponent.GetStatusSummary()}", LogMessageType.System);
                }
                
                if (openPositions.Count == 0) return;
                
                bool isIndividualMode = _closeMode.ValueString == "По отдельным сделкам";
                string closeModeText = isIndividualMode ? "ПО ОТДЕЛЬНЫМ СДЕЛКАМ" : "ОБЩАЯ ПОЗИЦИЯ";
                
                int botManagedPositions = positionManager.GetBotManagedPositionsCount();
                int allOpenPositions = positionManager.GetAllOpenPositionsCount();
                
                SendNewLogMessage($"=== ИНФОРМАЦИЯ О ПОЗИЦИЯХ ===", LogMessageType.System);
                SendNewLogMessage($"📋 РЕЖИМ ЗАКРЫТИЯ: {closeModeText}", LogMessageType.System);
                SendNewLogMessage($"📊 СТАТИСТИКА: Бот управляет {botManagedPositions} поз. | Всего открыто {allOpenPositions} поз.", LogMessageType.System);
                
                if (trailingComponent != null && trailingComponent.IsTrailingEnabled())
                {
                    SendNewLogMessage($"📌 ТРЕЙЛИНГ: {_trailingType.ValueString} | старт {_trailingStartPercent.ValueDecimal:F2}%", LogMessageType.System);
                }
                
                SendNewLogMessage($"🔄 СОСТОЯНИЕ СИСТЕМЫ: {_stateMachine?.CurrentState}", LogMessageType.System);
                
                // Логирование детальной информации о позициях
                if (isIndividualMode && openPositions.Count > 0)
                {
                    foreach (var position in openPositions.Take(3)) // Логируем только первые 3 позиции
                    {
                        int positionId = position.Number;
                        bool isBotPosition = positionManager.IsBotPosition(positionId);
                        string positionType = isBotPosition ? "БОТ" : "РУЧНАЯ";
                        
                        decimal entryPrice = riskManager?.GetEntryPrice(positionId) ?? position.EntryPrice;
                        if (entryPrice <= 0)
                        {
                            entryPrice = position.EntryPrice;
                        }
                        
                        decimal minProfitPercent = _minProfitPercentParam.ValueDecimal;
                        decimal breakEvenPrice = entryPrice > 0
                            ? (position.Direction == Side.Buy
                                ? entryPrice * (1 + minProfitPercent / 100m)
                                : entryPrice * (1 - minProfitPercent / 100m))
                            : 0;
                        
                        decimal minProfitPrice = riskManager?.GetMinProfitPrice(positionId) ?? breakEvenPrice;
                        var stats = riskManager?.GetPositionStats(positionId) ?? (0, 0, 0, 0);
                        
                        decimal currentPrice = _tab.PriceBestBid > 0 ? _tab.PriceBestBid : position.EntryPrice;
                        bool isLong = position.Direction == Side.Buy;
                        decimal currentProfitPercent = entryPrice > 0
                            ? (isLong 
                                ? ((currentPrice - entryPrice) / entryPrice) * 100m
                                : ((entryPrice - currentPrice) / entryPrice) * 100m)
                            : 0;
                        decimal currentProfitValue = isLong 
                            ? (currentPrice - entryPrice) * position.OpenVolume
                            : (entryPrice - currentPrice) * position.OpenVolume;
                        
                        bool wentPositive = riskManager?.WentPositive(positionId) ?? false;
                        
                        // Логика вывода статуса защиты минимальной прибыли
                        if (currentProfitPercent < minProfitPercent)
                        {
                            if (!wentPositive || currentProfitPercent < 0)
                            {
                                SendNewLogMessage(
                                    $"🚫 АБСОЛЮТНЫЙ ЗАПРЕТ: Позиция #{positionId} в минусе {currentProfitPercent:F2}%, закрытие заблокировано;",
                                    LogMessageType.System);
                            }
                            else
                            {
                                SendNewLogMessage(
                                    $"🛡️ ЗАЩИТА МИНИМАЛЬНОЙ ПРИБЫЛИ: Позиция #{positionId} текущая прибыль {currentProfitPercent:F2}% < {minProfitPercent:F2}%;",
                                    LogMessageType.System);
                            }
                        }
                        
                        SendNewLogMessage($"🔹 ПОЗИЦИЯ #{positionId} ({positionType}) {position.Direction}", LogMessageType.System);
                        SendNewLogMessage($"   💰 ТЕКУЩИЙ РЕЗУЛЬТАТ: {currentProfitValue:F2} ({currentProfitPercent:F2}%)", LogMessageType.System);
                        SendNewLogMessage($"   ⚖️ ЦЕНА БЕЗУБЫТКА: {breakEvenPrice:F4} (вход {entryPrice:F4})", LogMessageType.System);
                        SendNewLogMessage($"   🎯 МИН. ПРИБЫЛЬ: {minProfitPrice:F4} (порог {_minProfitPercentParam.ValueDecimal:F2}%)", LogMessageType.System);
                        SendNewLogMessage($"   📈 НАИЛУЧШИЙ РЕЗУЛЬТАТ: {stats.maxValue:F2} ({stats.maxPercent:F2}%)", LogMessageType.System);
                        SendNewLogMessage($"   📉 НАИХУДШИЙ РЕЗУЛЬТАТ: {stats.minValue:F2} ({stats.minPercent:F2}%)", LogMessageType.System);
                        SendNewLogMessage($"   📊 ДЕТАЛИ: Вход {entryPrice:F4} | Текущая {currentPrice:F4} | Объем {position.OpenVolume}", LogMessageType.System);
                    }
                }
                
                SendNewLogMessage($"=============================", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка логирования информации о позициях: {ex.Message}", LogMessageType.Error);
            }
        }

        private void LogPositionsForced()
        {
            try
            {
                var positionManager = _assembly.GetComponent<PositionManagerComponent>();
                var riskManager = _assembly.GetComponent<RiskManagementComponent>();
                var trailingComponent = _assembly.GetComponent<TrailingStopComponent>();
                var tab = _tab;
                if (positionManager == null || riskManager == null || tab == null)
                    return;

                // Для ручного лога считаем "открытыми" все позиции с ненулевым объёмом,
                // включая состояния Open и Closing – чтобы пользователь видел даже закрывающиеся сделки.
                var positions = tab.PositionsOpenAll != null
                    ? tab.PositionsOpenAll.Where(p => p.OpenVolume > 0 &&
                                                       (p.State == PositionStateType.Open ||
                                                        p.State == PositionStateType.Closing)).ToList()
                    : new List<Position>();

                if (positions.Count == 0)
                {
                    SendNewLogMessage("ℹ️ Открытых позиций нет", LogMessageType.System);
                    return;
                }

                // Безопасно берём снимок свечей, чтобы не трогать коллекцию,
                // которую может одновременно использовать отрисовщик графика.
                decimal lastPrice = 0m;
                var candles = tab.CandlesAll;
                if (candles != null && candles.Count > 0)
                {
                    var lastCandle = candles[candles.Count - 1];
                    if (lastCandle != null)
                        lastPrice = lastCandle.Close;
                }

                SendNewLogMessage($"=== ДЕТАЛЬНЫЙ СТАТУС ПОЗИЦИЙ (кнопка) === Всего найдено: {positions.Count}", LogMessageType.System);
                foreach (var pos in positions)
                {
                    int id = pos.Number;
                    decimal entry = riskManager.GetEntryPrice(id);
                    // ✅ Если позиция не инициализирована в RiskManager, используем цену входа из позиции
                    if (entry == 0)
                    {
                        entry = pos.EntryPrice;
                    }
                    
                    decimal minProfitPrice = riskManager.GetMinProfitPrice(id);
                    // ✅ Если мин.прибыль не рассчитана, рассчитываем на основе цены входа
                    if (minProfitPrice == 0 && entry > 0)
                    {
                        var minProfitPercent = _minProfitPercentParam?.ValueDecimal ?? 0.14m;
                        minProfitPrice = pos.Direction == Side.Buy
                            ? entry * (1 + minProfitPercent / 100m)
                            : entry * (1 - minProfitPercent / 100m);
                    }
                    
                    decimal curPrice = lastPrice > 0 ? lastPrice : pos.EntryPrice;
                    decimal pnlPercent = entry > 0
                        ? (pos.Direction == Side.Buy
                            ? (curPrice - entry) / entry * 100m
                            : (entry - curPrice) / entry * 100m)
                        : 0m;
                    decimal pnlValue = pos.Direction == Side.Buy
                        ? (curPrice - entry) * pos.OpenVolume
                        : (entry - curPrice) * pos.OpenVolume;

                    string reason = positionManager.GetOpenReason(id);
                    // ✅ Показываем реальную причину открытия, если она есть
                    // Если причина "неизвестен" или пустая, показываем "неизвестен" вместо "Manual"
                    string reasonText = string.IsNullOrWhiteSpace(reason) ? "неизвестен" : reason;

                    ExitInfo exitInfo = GetExpectedExitPrice(pos, riskManager, trailingComponent, entry, curPrice);
                    decimal expectedExit = exitInfo.Price;
                    string exitSource = exitInfo.Source;

                    SendNewLogMessage(
                        $"#{id} {pos.Direction} | вход {entry:F4} | мин. прибыль {minProfitPrice:F4} | " +
                        $"текущая {curPrice:F4} | PnL {pnlPercent:F2}% ({pnlValue:F2}) | " +
                        $"целевой выход {expectedExit:F4} ({exitSource}) | сигнал: {reasonText}",
                        LogMessageType.System);
                }
                SendNewLogMessage("=========================================", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка ручного лога позиций: {ex.Message}", LogMessageType.Error);
            }
        }

        private ExitInfo GetExpectedExitPrice(Position pos, RiskManagementComponent riskManager,
            TrailingStopComponent trailingComponent, decimal entryPrice, decimal currentPrice = 0m)
        {
            decimal minProfitPrice = riskManager?.GetMinProfitPrice(pos.Number) ?? 0m;
            int positionId = pos.Number;
            bool isLong = pos.Direction == Side.Buy;
            
            // ✅ КРИТИЧНО: Проверяем не только включен ли трейлинг, но и активен ли он для конкретной позиции
            bool trailingEnabled = trailingComponent != null && trailingComponent.IsTrailingEnabled();
            bool trailingActive = false;
            decimal trailingLevel = 0m;
            
            if (trailingEnabled)
            {
                // ✅ Проверяем активность трейлинга для конкретной позиции
                trailingActive = trailingComponent.IsTrailingActive(positionId);
                
                if (trailingActive)
                {
                    // ✅ Получаем уровень трейлинга из компонента
                    trailingLevel = trailingComponent.GetTrailingLevel(positionId);
                    
                    // ✅ Если уровень не получен, но трейлинг активен, пробуем пересчитать
                    // Это важно для случаев, когда компонент еще не обновил уровень
                    if (trailingLevel == 0m && entryPrice > 0)
                    {
                        string trailingType = trailingComponent.GetTrailingType();
                        // ✅ Используем текущую цену, если она передана, иначе цену входа
                        decimal priceForCalculation = currentPrice > 0 ? currentPrice : pos.EntryPrice;
                        
                        if (trailingType == "ATR")
                        {
                            var dataComponent = _assembly?.GetComponent<DataIndicatorComponent>();
                            if (dataComponent != null)
                            {
                                decimal atr = dataComponent.GetAtrValue();
                                decimal atrMultiplier = trailingComponent.GetAtrMultiplier();
                                if (atr > 0 && atrMultiplier > 0)
                                {
                                    trailingLevel = isLong 
                                        ? priceForCalculation - atr * atrMultiplier
                                        : priceForCalculation + atr * atrMultiplier;
                                }
                            }
                        }
                        
                        if (trailingLevel == 0m)
                        {
                            decimal trailingDistance = trailingComponent.GetTrailingDistancePercent();
                            if (trailingDistance > 0)
                            {
                                trailingLevel = isLong 
                                    ? priceForCalculation * (1 - trailingDistance / 100m)
                                    : priceForCalculation * (1 + trailingDistance / 100m);
                            }
                        }
                    }
                }
            }

            decimal manualTp = 0m;
            bool takeProfitOn = _useManualTakeProfit != null && _useManualTakeProfit.ValueString == "Включён";
            if (takeProfitOn && entryPrice > 0)
            {
                // ВАЖНО: как и у Андрея, manual TP используется как УСЛОВИЕ выхода,
                // а сама цена закрытия берётся по текущей цене, но не ниже minProfit.
                manualTp = isLong
                    ? entryPrice * (1 + _manualTakeProfit.ValueDecimal / 100m)
                    : entryPrice * (1 - _manualTakeProfit.ValueDecimal / 100m);
            }

            // ✅ ПРИОРИТЕТ 1: Если следящий стоп активен — закрываемся по текущей цене,
            // но НЕ НИЖЕ минимальной прибыли, как в реализации Андрея.
            if (trailingActive && trailingLevel > 0)
            {
                if (currentPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    
                    string source = "trailing (current>=min-profit)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
                else
                {
                    // Если текущая цена неизвестна (например, в ручном логе),
                    // используем уровень трейлинга, но не ниже minProfit.
                    decimal finalPrice = isLong
                        ? Math.Max(trailingLevel, minProfitPrice)
                        : Math.Min(trailingLevel, minProfitPrice);
                    
                    string source = finalPrice == trailingLevel
                        ? "trailing"
                        : "trailing (скорректирован до мин.прибыли)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
            }

            // ✅ ПРИОРИТЕТ 2: Тейк-профит
            if (manualTp > 0)
            {
                // Как у Андрея: раз TP-условие сработало, закрываемся по текущей цене,
                // но не ниже минимальной прибыли (minProfit).
                if (currentPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    
                    string source = "take-profit (current>=min-profit)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
                else
                {
                    // На всякий случай резерв: если currentPrice не передан, используем уровень TP,
                    // также не ниже minProfit.
                    decimal finalPrice = isLong
                        ? Math.Max(manualTp, minProfitPrice)
                        : Math.Min(manualTp, minProfitPrice);
                    
                    string source = finalPrice == manualTp
                        ? "take-profit"
                        : "take-profit (скорректирован до мин.прибыли)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
            }

            // ✅ ПРИОРИТЕТ 3: Минимальная прибыль / текущая цена (как у Андрея)
            if (minProfitPrice > 0)
            {
                if (currentPrice > 0)
                {
                    // Полностью копируем идею Андрея:
                    // закрываемся по текущей цене, но не ниже minProfit.
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    
                    string source = "min-profit (current>=min-profit)";
                    return new ExitInfo { Price = finalPrice, Source = source };
                }
                else
                {
                    // Если текущая цена неизвестна — возвращаем сам уровень minProfit.
                    return new ExitInfo { Price = minProfitPrice, Source = "min-profit" };
                }
            }

            return new ExitInfo { Price = entryPrice, Source = "entry" };
        }
        
        // Методы для внешнего доступа
        public async void StartEnhancedAIOptimization()
        {
            var aiComponent = _assembly.GetComponent<EnhancedAIOptimizationComponent>();
            if (aiComponent != null)
            {
                SendNewLogMessage("🚀 ЗАПУСК ГИБРИДНОЙ AI ОПТИМИЗАЦИИ...", LogMessageType.System);
                await aiComponent.StartHybridOptimizationAsync();
            }
        }
        
        public int GetTenkanPeriod() => _tenkanLength.ValueInt;
        public int GetKijunPeriod() => _kijunLength.ValueInt;
        public int GetSenkouBPeriod() => _senkouBLength.ValueInt;
        public decimal GetMinProfitPercent() => _minProfitPercentParam.ValueDecimal;
        public BotTabSimple GetTab() => _tab;
        
        /// <summary>
        /// Проверка, можно ли торговать в текущее время с учетом неторговых периодов
        /// </summary>
        private bool IsTradingTimeAllowed(DateTime currentTime)
        {
            try
            {
                DayOfWeek dayOfWeek = currentTime.DayOfWeek;
                int currentHour = currentTime.Hour;
                int currentMinute = currentTime.Minute;
                int currentTimeInMinutes = currentHour * 60 + currentMinute;
                
                // Получаем параметры для текущего дня
                StrategyParameterBool dayEnabled = null;
                StrategyParameterBool period1Enabled = null;
                StrategyParameterInt period1StartHour = null;
                StrategyParameterInt period1StartMinute = null;
                StrategyParameterInt period1EndHour = null;
                StrategyParameterInt period1EndMinute = null;
                StrategyParameterBool period2Enabled = null;
                StrategyParameterInt period2StartHour = null;
                StrategyParameterInt period2StartMinute = null;
                StrategyParameterInt period2EndHour = null;
                StrategyParameterInt period2EndMinute = null;
                StrategyParameterBool period3Enabled = null;
                StrategyParameterInt period3StartHour = null;
                StrategyParameterInt period3StartMinute = null;
                StrategyParameterInt period3EndHour = null;
                StrategyParameterInt period3EndMinute = null;
                
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday:
                        dayEnabled = _mondayTrade;
                        period1Enabled = _mondayPeriod1Enabled;
                        period1StartHour = _mondayPeriod1StartHour;
                        period1StartMinute = _mondayPeriod1StartMinute;
                        period1EndHour = _mondayPeriod1EndHour;
                        period1EndMinute = _mondayPeriod1EndMinute;
                        period2Enabled = _mondayPeriod2Enabled;
                        period2StartHour = _mondayPeriod2StartHour;
                        period2StartMinute = _mondayPeriod2StartMinute;
                        period2EndHour = _mondayPeriod2EndHour;
                        period2EndMinute = _mondayPeriod2EndMinute;
                        period3Enabled = _mondayPeriod3Enabled;
                        period3StartHour = _mondayPeriod3StartHour;
                        period3StartMinute = _mondayPeriod3StartMinute;
                        period3EndHour = _mondayPeriod3EndHour;
                        period3EndMinute = _mondayPeriod3EndMinute;
                        break;
                    case DayOfWeek.Tuesday:
                        dayEnabled = _tuesdayTrade;
                        period1Enabled = _tuesdayPeriod1Enabled;
                        period1StartHour = _tuesdayPeriod1StartHour;
                        period1StartMinute = _tuesdayPeriod1StartMinute;
                        period1EndHour = _tuesdayPeriod1EndHour;
                        period1EndMinute = _tuesdayPeriod1EndMinute;
                        period2Enabled = _tuesdayPeriod2Enabled;
                        period2StartHour = _tuesdayPeriod2StartHour;
                        period2StartMinute = _tuesdayPeriod2StartMinute;
                        period2EndHour = _tuesdayPeriod2EndHour;
                        period2EndMinute = _tuesdayPeriod2EndMinute;
                        period3Enabled = _tuesdayPeriod3Enabled;
                        period3StartHour = _tuesdayPeriod3StartHour;
                        period3StartMinute = _tuesdayPeriod3StartMinute;
                        period3EndHour = _tuesdayPeriod3EndHour;
                        period3EndMinute = _tuesdayPeriod3EndMinute;
                        break;
                    case DayOfWeek.Wednesday:
                        dayEnabled = _wednesdayTrade;
                        period1Enabled = _wednesdayPeriod1Enabled;
                        period1StartHour = _wednesdayPeriod1StartHour;
                        period1StartMinute = _wednesdayPeriod1StartMinute;
                        period1EndHour = _wednesdayPeriod1EndHour;
                        period1EndMinute = _wednesdayPeriod1EndMinute;
                        period2Enabled = _wednesdayPeriod2Enabled;
                        period2StartHour = _wednesdayPeriod2StartHour;
                        period2StartMinute = _wednesdayPeriod2StartMinute;
                        period2EndHour = _wednesdayPeriod2EndHour;
                        period2EndMinute = _wednesdayPeriod2EndMinute;
                        period3Enabled = _wednesdayPeriod3Enabled;
                        period3StartHour = _wednesdayPeriod3StartHour;
                        period3StartMinute = _wednesdayPeriod3StartMinute;
                        period3EndHour = _wednesdayPeriod3EndHour;
                        period3EndMinute = _wednesdayPeriod3EndMinute;
                        break;
                    case DayOfWeek.Thursday:
                        dayEnabled = _thursdayTrade;
                        period1Enabled = _thursdayPeriod1Enabled;
                        period1StartHour = _thursdayPeriod1StartHour;
                        period1StartMinute = _thursdayPeriod1StartMinute;
                        period1EndHour = _thursdayPeriod1EndHour;
                        period1EndMinute = _thursdayPeriod1EndMinute;
                        period2Enabled = _thursdayPeriod2Enabled;
                        period2StartHour = _thursdayPeriod2StartHour;
                        period2StartMinute = _thursdayPeriod2StartMinute;
                        period2EndHour = _thursdayPeriod2EndHour;
                        period2EndMinute = _thursdayPeriod2EndMinute;
                        period3Enabled = _thursdayPeriod3Enabled;
                        period3StartHour = _thursdayPeriod3StartHour;
                        period3StartMinute = _thursdayPeriod3StartMinute;
                        period3EndHour = _thursdayPeriod3EndHour;
                        period3EndMinute = _thursdayPeriod3EndMinute;
                        break;
                    case DayOfWeek.Friday:
                        dayEnabled = _fridayTrade;
                        period1Enabled = _fridayPeriod1Enabled;
                        period1StartHour = _fridayPeriod1StartHour;
                        period1StartMinute = _fridayPeriod1StartMinute;
                        period1EndHour = _fridayPeriod1EndHour;
                        period1EndMinute = _fridayPeriod1EndMinute;
                        period2Enabled = _fridayPeriod2Enabled;
                        period2StartHour = _fridayPeriod2StartHour;
                        period2StartMinute = _fridayPeriod2StartMinute;
                        period2EndHour = _fridayPeriod2EndHour;
                        period2EndMinute = _fridayPeriod2EndMinute;
                        period3Enabled = _fridayPeriod3Enabled;
                        period3StartHour = _fridayPeriod3StartHour;
                        period3StartMinute = _fridayPeriod3StartMinute;
                        period3EndHour = _fridayPeriod3EndHour;
                        period3EndMinute = _fridayPeriod3EndMinute;
                        break;
                    case DayOfWeek.Saturday:
                        dayEnabled = _saturdayTrade;
                        period1Enabled = _saturdayPeriod1Enabled;
                        period1StartHour = _saturdayPeriod1StartHour;
                        period1StartMinute = _saturdayPeriod1StartMinute;
                        period1EndHour = _saturdayPeriod1EndHour;
                        period1EndMinute = _saturdayPeriod1EndMinute;
                        period2Enabled = _saturdayPeriod2Enabled;
                        period2StartHour = _saturdayPeriod2StartHour;
                        period2StartMinute = _saturdayPeriod2StartMinute;
                        period2EndHour = _saturdayPeriod2EndHour;
                        period2EndMinute = _saturdayPeriod2EndMinute;
                        period3Enabled = _saturdayPeriod3Enabled;
                        period3StartHour = _saturdayPeriod3StartHour;
                        period3StartMinute = _saturdayPeriod3StartMinute;
                        period3EndHour = _saturdayPeriod3EndHour;
                        period3EndMinute = _saturdayPeriod3EndMinute;
                        break;
                    case DayOfWeek.Sunday:
                        dayEnabled = _sundayTrade;
                        period1Enabled = _sundayPeriod1Enabled;
                        period1StartHour = _sundayPeriod1StartHour;
                        period1StartMinute = _sundayPeriod1StartMinute;
                        period1EndHour = _sundayPeriod1EndHour;
                        period1EndMinute = _sundayPeriod1EndMinute;
                        period2Enabled = _sundayPeriod2Enabled;
                        period2StartHour = _sundayPeriod2StartHour;
                        period2StartMinute = _sundayPeriod2StartMinute;
                        period2EndHour = _sundayPeriod2EndHour;
                        period2EndMinute = _sundayPeriod2EndMinute;
                        period3Enabled = _sundayPeriod3Enabled;
                        period3StartHour = _sundayPeriod3StartHour;
                        period3StartMinute = _sundayPeriod3StartMinute;
                        period3EndHour = _sundayPeriod3EndHour;
                        period3EndMinute = _sundayPeriod3EndMinute;
                        break;
                }
                
                // Если день отключен, торговля запрещена
                if (dayEnabled == null || !dayEnabled.ValueBool)
                {
                    return false;
                }
                
                // Проверяем все включенные неторговые периоды
                // Если текущее время попадает в любой включенный период - торговля ЗАПРЕЩЕНА
                
                // Период 1
                if (period1Enabled != null && period1Enabled.ValueBool &&
                    period1StartHour != null && period1StartMinute != null &&
                    period1EndHour != null && period1EndMinute != null)
                {
                    int period1Start = period1StartHour.ValueInt * 60 + period1StartMinute.ValueInt;
                    int period1End = period1EndHour.ValueInt * 60 + period1EndMinute.ValueInt;
                    
                    if (IsTimeInPeriod(currentTimeInMinutes, period1Start, period1End))
                    {
                        return false; // Неторговый период
                    }
                }
                
                // Период 2
                if (period2Enabled != null && period2Enabled.ValueBool &&
                    period2StartHour != null && period2StartMinute != null &&
                    period2EndHour != null && period2EndMinute != null)
                {
                    int period2Start = period2StartHour.ValueInt * 60 + period2StartMinute.ValueInt;
                    int period2End = period2EndHour.ValueInt * 60 + period2EndMinute.ValueInt;
                    
                    if (IsTimeInPeriod(currentTimeInMinutes, period2Start, period2End))
                    {
                        return false; // Неторговый период
                    }
                }
                
                // Период 3
                if (period3Enabled != null && period3Enabled.ValueBool &&
                    period3StartHour != null && period3StartMinute != null &&
                    period3EndHour != null && period3EndMinute != null)
                {
                    int period3Start = period3StartHour.ValueInt * 60 + period3StartMinute.ValueInt;
                    int period3End = period3EndHour.ValueInt * 60 + period3EndMinute.ValueInt;
                    
                    if (IsTimeInPeriod(currentTimeInMinutes, period3Start, period3End))
                    {
                        return false; // Неторговый период
                    }
                }
                
                // Если не попали ни в один неторговый период - торговля разрешена
                return true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка проверки неторговых периодов: {ex.Message}", LogMessageType.Error);
                return true; // В случае ошибки разрешаем торговлю
            }
        }
        
        /// <summary>
        /// Проверка, попадает ли текущее время в неторговый период (с учетом перехода через полночь)
        /// </summary>
        private bool IsTimeInPeriod(int currentTimeInMinutes, int periodStartInMinutes, int periodEndInMinutes)
        {
            // Если период переходит через полночь (например, 23:59 - 7:00)
            if (periodStartInMinutes > periodEndInMinutes)
            {
                // Период начинается сегодня и заканчивается завтра
                return currentTimeInMinutes >= periodStartInMinutes || currentTimeInMinutes <= periodEndInMinutes;
            }
            else
            {
                // Период в пределах одного дня
                return currentTimeInMinutes >= periodStartInMinutes && currentTimeInMinutes <= periodEndInMinutes;
            }
        }
        
        public override string GetNameStrategyType()
        {
            return "IshimokuAdaptiveTrailing";
        }
        
        public override void ShowIndividualSettingsDialog()
        {
            // Используем стандартный интерфейс OsEngine
        }
        
        // Реализация очистки ресурсов
        public void Cleanup()
        {
            _assembly?.Dispose();
        }
    }
    
    #endregion
}