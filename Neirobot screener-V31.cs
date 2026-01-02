using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsEngine.Indicators;

namespace OsEngine.Robots.Engines
{
    [Bot("UniversalScreenerEngine")]
    public class UniversalScreenerEngine : BotPanel
    {
        public UniversalScreenerEngine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            Description = "Универсальный скринер с Ишимоку, улучшенным алгоритмом усреднения, системой задержек и генетическим программированием";

            // === РЕЖИМЫ ТОРГОВЛИ ===
            TradingMode = CreateParameter("Режим торговли", "On", new[] { "On", "Off", "Only Close Position" });
            EnableLong = CreateParameter("Включить Лонг", true);
            EnableShort = CreateParameter("Включить Шорт", false);

            // === ОСНОВНЫЕ ПАРАМЕТРЫ ===
            MaxTradingInstruments = CreateParameter("Макс. инструментов", 5, 1, 50, 1);
            VolumeReductionPerOrder = CreateParameter("Уменьшение объема %", 5m, 0m, 20m, 1m);
            MaxOrdersCount = CreateParameter("Макс. ордеров", 10, 1, 20, 1);
            
            // === ПАРАМЕТРЫ ГЕНЕТИЧЕСКОГО ПРОГРАММИРОВАНИЯ ===
            EnableGeneticOptimization = CreateParameter("Включить генетическую оптимизацию", false);
            GeneticPopulationSize = CreateParameter("Размер популяции", 50, 20, 200, 10);
            GeneticGenerations = CreateParameter("Количество поколений", 100, 50, 500, 50);
            GeneticMutationRate = CreateParameter("Вероятность мутации %", 0.1m, 0.01m, 1.0m, 0.05m);
            GeneticCrossoverRate = CreateParameter("Вероятность кроссовера %", 0.8m, 0.5m, 1.0m, 0.1m);
            GeneticOptimizationPeriod = CreateParameter("Период оптимизации (дней)", 30, 10, 365, 10);
            GeneticFitnessFunction = CreateParameter("Функция приспособленности", "SharpeRatio", 
                new[] { "TotalProfit", "SharpeRatio", "ProfitFactor", "WinRate", "CustomComposite" });
            UseRealTimeGeneticOptimization = CreateParameter("Реал-тайм оптимизация", false);
            GeneticOptimizationInterval = CreateParameter("Интервал оптимизации (дней)", 7, 1, 30, 1);

            // === ПАРАМЕТРЫ РАССТОЯНИЯ ===
            DistanceBetweenLongOrders = CreateParameter("Расстояние Лонг %", 0.3m, 0.1m, 1m, 0.1m);
            TakeProfitLong = CreateParameter("Тейк-профит Лонг %", 0.5m, 0.2m, 1m, 0.1m);
            DistanceBetweenShortOrders = CreateParameter("Расстояние Шорт %", 0.3m, 0.1m, 1m, 0.1m);
            TakeProfitShort = CreateParameter("Тейк-профит Шорт %", 0.5m, 0.2m, 1m, 0.1m);

            // === ИНДИКАТОР ИШИМОКУ (ЗАМЕНА EMA) ===
            IchimokuTenkanPeriod = CreateParameter("Ишимоку Тенкан", 9, 5, 20, 1);
            IchimokuKijunPeriod = CreateParameter("Ишимоку Киджун", 26, 15, 50, 1);
            IchimokuSenkouBPeriod = CreateParameter("Ишимоку Сенкоу B", 52, 40, 60, 1);
            IchimokuDisplacement = CreateParameter("Ишимоку Сдвиг", 26, 20, 30, 1);
            
            UseTrendFilter = CreateParameter("Фильтр тренда", true);
            AtrPeriod = CreateParameter("Период ATR", 14, 10, 20, 2);
            UseDynamicDistance = CreateParameter("Динамическое расстояние", true);
            BaseDistanceAtrMultiplier = CreateParameter("Базовый множитель ATR", 0.5m, 0.1m, 1.0m, 0.1m);
            RsiPeriod = CreateParameter("Период RSI", 14, 7, 21, 1);
            UseRsiFilter = CreateParameter("Фильтр RSI", false); // Temporarily disabled for testing
            RsiOverbought = CreateParameter("RSI перекупленность", 75m, 60m, 85m, 2m);
            RsiOversold = CreateParameter("RSI перепроданность", 25m, 15m, 40m, 2m);

            // === НОВЫЕ ПАРАМЕТРЫ ДЛЯ ГРАДУИРОВАННОЙ БЛОКИРОВКИ ШОРТОВ ===
            UseGradedShortBlocking = CreateParameter("Градуированная блокировка шортов", false); // Temporarily disabled for testing
            ShortBlockStrengthThreshold1 = CreateParameter("Порог блокировки шортов 1%", 50m, 30m, 90m, 5m);
            ShortBlockStrengthThreshold2 = CreateParameter("Порог блокировки шортов 2%", 70m, 50m, 95m, 5m);
            ShortBlockStrengthThreshold3 = CreateParameter("Порог блокировки шортов 3%", 90m, 70m, 99m, 5m);
            ShortVolumeReduction1 = CreateParameter("Снижение объема шортов 1%", 30m, 10m, 50m, 5m);
            ShortVolumeReduction2 = CreateParameter("Снижение объема шортов 2%", 60m, 40m, 80m, 5m);
            ShortVolumeReduction3 = CreateParameter("Снижение объема шортов 3%", 90m, 70m, 100m, 5m);

            // === ДОПОЛНИТЕЛЬНЫЕ ФИЛЬТРЫ ТРЕНДА ===
            UseVolatilityFilter = CreateParameter("Фильтр волатильности", false); // Temporarily disabled for testing
            MaxVolatilityForShorts = CreateParameter("Макс. волатильность для шортов%", 5m, 2m, 10m, 1m);

            // === РАЗНЫЕ НАСТРОЙКИ ЗАДЕРЖЕК ДЛЯ ШОРТОВ ===
            UseDifferentShortDelays = CreateParameter("Разные задержки для шортов", true);
            ShortDelayMultiplierUptrend = CreateParameter("Множитель задержки шортов в аптренде", 2.0m, 1.0m, 5.0m, 0.5m);
            ShortDelayMultiplierStrongUptrend = CreateParameter("Множитель задержки шортов в сильном аптренде", 3.0m, 1.5m, 8.0m, 0.5m);

            // === ЗАЩИТНЫЕ МЕХАНИЗМЫ ===
            MaxDrawdownPerSecurity = CreateParameter("Макс. просадка на инструмент %", 2m, 0.5m, 5m, 0.1m);
            UseDrawdownProtection = CreateParameter("Защита от просадки", true);
            MaxDrawdownPerInstrument = CreateParameter("Макс. просадка инструмента %", 3m, 1m, 10m, 0.5m);
            VolumeReductionFactor = CreateParameter("Коэф. снижения объема", 0.5m, 0.2m, 0.8m, 0.1m);

            // === ПАРАМЕТРЫ БЕЗУБЫТОЧНОГО ЗАКРЫТИЯ ===
            MinProfitPercent = CreateParameter("Минимальная прибыль %", 0.14m, 0m, 5m, 0.01m);

            // === ПАРАМЕТРЫ БЭКТЕСТИНГА ===
            EnableBacktesting = CreateParameter("Включить бэктестинг", false);
            BacktestDataSource = CreateParameter("Источник данных бэктеста", "OsEngine", new[] { "OsEngine", "TextFiles" });
            // Отключаем загрузку из файлов по умолчанию - используем встроенные данные OsEngine
            BacktestFilesPath = CreateParameter("Путь к файлам данных", "Data");
            BacktestPeriodDays = CreateParameter("Период бэктеста (дни)", 365, 30, 1000, 30);
            BacktestStepMinutes = CreateParameter("Шаг бэктеста (мин)", 5, 1, 60, 1);
            BacktestMinTrades = CreateParameter("Мин. сделок для оценки", 10, 5, 100, 5);
            ApplyBacktestResults = CreateParameter("Применять результаты бэктеста", true);


            UseTrailingStop = CreateParameter("Использовать трейлинг-стоп", true);
            TrailingType = CreateParameter("Тип трейлинга", "Fixed", new[] { "Fixed", "ATR", "Adaptive", "SelfLearning" });
            TrailingDistancePercent = CreateParameter("Расстояние трейлинга %", 5m, 0.1m, 20m, 0.5m);
            TrailingAtrMultiplier = CreateParameter("Множитель ATR", 1.5m, 0.5m, 3.0m, 0.1m);
            AdaptiveDistance = CreateParameter("Адаптивное расстояние", 3m, 0.5m, 10m, 0.5m);

            // === САМООБУЧАЕМЫЙ ТРЕЙЛИНГ ===
            SelfLearningMinTrades = CreateParameter("Мин. сделок для обучения", 10, 5, 50, 5);
            SelfLearningAdaptationRate = CreateParameter("Скорость адаптации", 0.1m, 0.01m, 0.5m, 0.01m);
            SelfLearningVolatilityWeight = CreateParameter("Вес волатильности", 0.4m, 0.1m, 0.8m, 0.1m);
            SelfLearningTrendWeight = CreateParameter("Вес тренда", 0.3m, 0.1m, 0.8m, 0.1m);
            SelfLearningProfitWeight = CreateParameter("Вес прибыли", 0.3m, 0.1m, 0.8m, 0.1m);
            SelfLearningBaseDistance = CreateParameter("Базовое расстояние самообучения %", 2m, 0.5m, 10m, 0.5m);

            // === ОБЪЕМ ===
            VolumeType = CreateParameter("Тип объема", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Объем", 20m, 1.0m, 50m, 4m);
            TradeAssetInPortfolio = CreateParameter("Актив портфеля", "Prime");
            Slippage = CreateParameter("Проскальзываение %", 0m, 0m, 20m, 1m);

            // === ЗАЩИТА ОТ ПАМПА И ДАМПА ===
            UsePumpProtection = CreateParameter("Защита от пампа", true);
            UseDumpProtection = CreateParameter("Защита от дампа", true);
            PumpDetectionSensitivity = CreateParameter("Чувствительность пампа", 3.0m, 1.5m, 5.0m, 0.1m);
            DumpDetectionSensitivity = CreateParameter("Чувствительность дампа", -3.0m, -5.0m, -1.5m, 0.1m);
            MinVolumeSpikeRatio = CreateParameter("Мин. скачок объема", 3.0m, 2.0m, 10.0m, 0.5m);
            BlockShortsOnPump = CreateParameter("Блокировать шорты при пампе", true);
            BlockLongsOnDump = CreateParameter("Блокировать лонги при дампе", true);
            EmergencyVolumeReduction = CreateParameter("Аварийное снижение объема", 0.3m, 0.1m, 0.8m, 0.1m);



            // === ЗАДЕРЖКИ ПЕРЕД СДЕЛКАМИ ===
            UseTradeDelay = CreateParameter("Использовать задержку", true);
            DelayBeforeOpenSeconds = CreateParameter("Задержка перед открытием (сек)", 3, 0, 30, 1);
            DelayBetweenOrdersSeconds = CreateParameter("Задержка между ордерами (сек)", 2, 0, 15, 1);
            RandomDelayRange = CreateParameter("Случайная добавка к задержке (сек)", 2, 0, 10, 1);

            // Additional parameters for signals
            UseCounterintuitive = CreateParameter("Использовать контртрендовую логику", "Включено", new[] { "Включено", "Отключено" });
            CounterintuitiveEntry = CreateParameter("Вход на контртренде", "Включено", new[] { "Включено", "Отключено" });
            OpenByTkKj = CreateParameter("Открытие по TkKj", "Включено", new[] { "Включено", "Отключено" });
            OpenByCloud = CreateParameter("Открытие по Cloud", "Включено", new[] { "Включено", "Отключено" });
            OpenByChikou = CreateParameter("Открытие по Chikou", "Включено", new[] { "Включено", "Отключено" });
Open6yTkKy = CreateParameter("Открытие по TkKy", "Включено", new[] { "Включено", "Отключено" });
Open6yCloud = CreateParameter("Открытие по Cloud (6y)", "Включено", new[] { "Включено", "Отключено" });
Open6yChikou = CreateParameter("Открытие по Chikou (6y)", "Включено", new[] { "Включено", "Отключено" });
Open6yIkKy = CreateParameter("Открытие по IkKy", "Включено", new[] { "Включено", "Отключено" });

            // Инициализация словарей
            _maxPrices = new Dictionary<string, decimal>();
            _minPrices = new Dictionary<string, decimal>();
            _lastLogTimes = new Dictionary<string, DateTime>();
            _lastTrendDirection = new Dictionary<string, string>();
            _activeInstruments = new Dictionary<string, DateTime>();
            _instrumentDrawdowns = new Dictionary<string, decimal>();
            _positionTakeProfits = new Dictionary<string, decimal>();
            _pumpDetectors = new Dictionary<string, PumpDetector>();
            _dumpDetectors = new Dictionary<string, DumpDetector>();
            


            // Система задержек
            _lastOrderTimes = new Dictionary<string, DateTime>();
            _pendingOrders = new Dictionary<string, PendingOrder>();
            _instrumentTrends = new Dictionary<string, TrendAnalysis>();

            // Генетическое программирование
            _geneticAlgorithm = new GeneticAlgorithm(this);
            _lastOptimizationTime = DateTime.MinValue;
            _optimizedParameters = new Dictionary<string, decimal>();
            _isOptimizationRunning = false;

            TabScreener.CandleFinishedEvent += TabScreener_CandleFinishedEvent;

            SendNewLogMessage("Робот UniversalScreenerEngine с Ишимоку, улучшенным усреднением, защитой от пампа/дампа, системой задержек и генетическим программированием инициализирован", LogMessageType.System);
        }

        #region ПАРАМЕТРЫ

        public BotTabScreener TabScreener => (BotTabScreener)TabsScreener[0];



        // Режимы торговли
        public StrategyParameterString TradingMode;
        public StrategyParameterBool EnableLong;
        public StrategyParameterBool EnableShort;

        // Основные параметры
        public StrategyParameterInt MaxTradingInstruments;
        public StrategyParameterDecimal VolumeReductionPerOrder;
        public StrategyParameterInt MaxOrdersCount;
        
        // Параметры генетического программирования
        public StrategyParameterBool EnableGeneticOptimization;
        public StrategyParameterInt GeneticPopulationSize;
        public StrategyParameterInt GeneticGenerations;
        public StrategyParameterDecimal GeneticMutationRate;
        public StrategyParameterDecimal GeneticCrossoverRate;
        public StrategyParameterInt GeneticOptimizationPeriod;
        public StrategyParameterString GeneticFitnessFunction;
        public StrategyParameterBool UseRealTimeGeneticOptimization;
        public StrategyParameterInt GeneticOptimizationInterval;

        // Параметры расстояния
        public StrategyParameterDecimal DistanceBetweenLongOrders;
        public StrategyParameterDecimal TakeProfitLong;
        public StrategyParameterDecimal DistanceBetweenShortOrders;
        public StrategyParameterDecimal TakeProfitShort;

        // ИНДИКАТОР ИШИМОКУ (ЗАМЕНА EMA)
        public StrategyParameterInt IchimokuTenkanPeriod;
        public StrategyParameterInt IchimokuKijunPeriod;
        public StrategyParameterInt IchimokuSenkouBPeriod;
        public StrategyParameterInt IchimokuDisplacement;
        
        public StrategyParameterBool UseTrendFilter;
        public StrategyParameterInt AtrPeriod;
        public StrategyParameterBool UseDynamicDistance;
        public StrategyParameterDecimal BaseDistanceAtrMultiplier;
        public StrategyParameterInt RsiPeriod;
        public StrategyParameterBool UseRsiFilter;
        public StrategyParameterDecimal RsiOverbought;
        public StrategyParameterDecimal RsiOversold;

        // Новые параметры для градуированной блокировки шортов
        public StrategyParameterBool UseGradedShortBlocking;
        public StrategyParameterDecimal ShortBlockStrengthThreshold1;
        public StrategyParameterDecimal ShortBlockStrengthThreshold2;
        public StrategyParameterDecimal ShortBlockStrengthThreshold3;
        public StrategyParameterDecimal ShortVolumeReduction1;
        public StrategyParameterDecimal ShortVolumeReduction2;
        public StrategyParameterDecimal ShortVolumeReduction3;

        // Дополнительные фильтры тренда
        public StrategyParameterBool UseVolatilityFilter;
        public StrategyParameterDecimal MaxVolatilityForShorts;

        // Разные настройки задержек для шортов
        public StrategyParameterBool UseDifferentShortDelays;
        public StrategyParameterDecimal ShortDelayMultiplierUptrend;
        public StrategyParameterDecimal ShortDelayMultiplierStrongUptrend;

        // Защитные механизмы
        public StrategyParameterDecimal MaxDrawdownPerSecurity;
        public StrategyParameterBool UseDrawdownProtection;
        public StrategyParameterDecimal MaxDrawdownPerInstrument;
        public StrategyParameterDecimal VolumeReductionFactor;

        // Объем
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterDecimal Slippage;

        // Защита от пампа и дампа
        public StrategyParameterBool UsePumpProtection;
        public StrategyParameterBool UseDumpProtection;
        public StrategyParameterDecimal PumpDetectionSensitivity;
        public StrategyParameterDecimal DumpDetectionSensitivity;
        public StrategyParameterDecimal MinVolumeSpikeRatio;
        public StrategyParameterBool BlockShortsOnPump;
        public StrategyParameterBool BlockLongsOnDump;
        public StrategyParameterDecimal EmergencyVolumeReduction;



        // Задержки перед сделками
        public StrategyParameterBool UseTradeDelay;
        public StrategyParameterInt DelayBeforeOpenSeconds;
        public StrategyParameterInt DelayBetweenOrdersSeconds;
        public StrategyParameterInt RandomDelayRange;

        // Параметры бэктестинга
        public StrategyParameterBool EnableBacktesting;
        public StrategyParameterString BacktestDataSource;
        public StrategyParameterString BacktestFilesPath;
        public StrategyParameterInt BacktestPeriodDays;
        public StrategyParameterInt BacktestStepMinutes;
        public StrategyParameterInt BacktestMinTrades;
        public StrategyParameterBool ApplyBacktestResults;

        // Параметры безубыточного закрытия
        public StrategyParameterDecimal MinProfitPercent;

        // Трейлинг-стоп
        public StrategyParameterBool UseTrailingStop;
        public StrategyParameterString TrailingType;
        public StrategyParameterDecimal TrailingDistancePercent;
        public StrategyParameterDecimal TrailingAtrMultiplier;
        public StrategyParameterDecimal AdaptiveDistance;

        // Самообучаемый трейлинг
        public StrategyParameterInt SelfLearningMinTrades;
        public StrategyParameterDecimal SelfLearningAdaptationRate;
        public StrategyParameterDecimal SelfLearningVolatilityWeight;
        public StrategyParameterDecimal SelfLearningTrendWeight;
        public StrategyParameterDecimal SelfLearningProfitWeight;
        public StrategyParameterDecimal SelfLearningBaseDistance;

        // Additional parameters for signals
        public StrategyParameterString UseCounterintuitive;
        public StrategyParameterString CounterintuitiveEntry;
        public StrategyParameterString OpenByTkKj;
        public StrategyParameterString OpenByCloud;
        public StrategyParameterString OpenByChikou;
        public StrategyParameterString Open6yTkKy;
        public StrategyParameterString Open6yCloud;
        public StrategyParameterString Open6yChikou;
        public StrategyParameterString Open6yIkKy;

        #endregion

        #region ПРИВАТНЫЕ ПЕРЕМЕННЫЕ

        private Dictionary<string, decimal> _maxPrices;
        private Dictionary<string, decimal> _minPrices;
        private Dictionary<string, DateTime> _lastLogTimes;
        private Dictionary<string, string> _lastTrendDirection;
        private Dictionary<string, DateTime> _activeInstruments;
        private Dictionary<string, decimal> _instrumentDrawdowns;
        private Dictionary<string, decimal> _positionTakeProfits;
        private Dictionary<string, PumpDetector> _pumpDetectors;
        private Dictionary<string, DumpDetector> _dumpDetectors;
        


        // Система задержек
        private Dictionary<string, DateTime> _lastOrderTimes;
        private Dictionary<string, PendingOrder> _pendingOrders;
        private Dictionary<string, TrendAnalysis> _instrumentTrends;

        // Генетическое программирование
        private GeneticAlgorithm _geneticAlgorithm;
        private DateTime _lastOptimizationTime;
        private Dictionary<string, decimal> _optimizedParameters;
        private bool _isOptimizationRunning;

        // Бэктестинг
        private Dictionary<string, decimal> _backtestBestParameters;
        private BacktestResult _backtestBestResult;

        // Трейлинг-стоп
        private Dictionary<int, decimal> _currentTrailingLevels = new Dictionary<int, decimal>();
        private Dictionary<int, decimal> _highestPricesSinceEntry = new Dictionary<int, decimal>();
        private Dictionary<int, decimal> _lowestPricesSinceEntry = new Dictionary<int, decimal>();

        // Самообучаемый трейлинг
        private Dictionary<string, TrailingLearningData> _trailingLearningData = new Dictionary<string, TrailingLearningData>();
        private Dictionary<int, TrailingHistory> _positionTrailingHistory = new Dictionary<int, TrailingHistory>();

        // Безубыточное закрытие
        private RiskManagementComponent _riskManager;
        private Dictionary<int, bool> _wentPositive = new Dictionary<int, bool>();

        #endregion

        #region КЛАССЫ ГЕНЕТИЧЕСКОГО ПРОГРАММИРОВАНИЯ

        /// <summary>
        /// Класс для представления особи (набора параметров) в генетическом алгоритме
        /// </summary>
        public class Individual
        {
            public Dictionary<string, decimal> Parameters { get; set; }
            public double Fitness { get; set; }
            public double SharpeRatio { get; set; }
            public double TotalProfit { get; set; }
            public double MaxDrawdown { get; set; }
            public double WinRate { get; set; }
            public int TradeCount { get; set; }
            public double ProfitFactor { get; set; }

            public Individual()
            {
                Parameters = new Dictionary<string, decimal>();
                Fitness = 0;
            }

            public Individual Clone()
            {
                var clone = new Individual();
                foreach (var param in Parameters)
                {
                    clone.Parameters[param.Key] = param.Value;
                }
                clone.Fitness = Fitness;
                return clone;
            }
        }

        /// <summary>
        /// Класс генетического алгоритма для оптимизации параметров
        /// </summary>
        public class GeneticAlgorithm
        {
            private readonly UniversalScreenerEngine _robot;
            private readonly Random _random;
            private List<Individual> _population;
            private Individual _bestIndividual;

            public GeneticAlgorithm(UniversalScreenerEngine robot)
            {
                _robot = robot;
                _random = new Random();
                _population = new List<Individual>();
                _bestIndividual = new Individual();
            }

            /// <summary>
            /// Инициализация начальной популяции
            /// </summary>
            public void InitializePopulation(int populationSize)
            {
                _population.Clear();

                for (int i = 0; i < populationSize; i++)
                {
                    var individual = CreateRandomIndividual();
                    _population.Add(individual);
                }

                _robot.SendNewLogMessage($"Инициализирована популяция из {_population.Count} особей", LogMessageType.System);
            }

            /// <summary>
            /// Создание случайной особи с оптимизированными параметрами
            /// </summary>
            private Individual CreateRandomIndividual()
            {
                var individual = new Individual();

                // === ОСНОВНЫЕ ПАРАМЕТРЫ ===
                individual.Parameters["VolumeReductionPerOrder"] = (decimal)(_random.NextDouble() * 20);
                individual.Parameters["MaxOrdersCount"] = _random.Next(1, 21);

                // === ИНДИКАТОР ИШИМОКУ (ЗАМЕНА EMA) ===
                individual.Parameters["IchimokuTenkanPeriod"] = _random.Next(5, 21);
                individual.Parameters["IchimokuKijunPeriod"] = _random.Next(15, 51);
                individual.Parameters["IchimokuSenkouBPeriod"] = _random.Next(40, 61);
                individual.Parameters["IchimokuDisplacement"] = _random.Next(20, 31);

                individual.Parameters["AtrPeriod"] = _random.Next(10, 21);
                individual.Parameters["BaseDistanceAtrMultiplier"] = (decimal)(_random.NextDouble() * 0.9 + 0.1);
                individual.Parameters["RsiPeriod"] = _random.Next(7, 22);
                individual.Parameters["RsiOverbought"] = _random.Next(60, 81);
                individual.Parameters["RsiOversold"] = _random.Next(20, 41);

                // === УПРОЩЕННАЯ БЛОКИРОВКА ШОРТОВ (2 параметра вместо 6) ===
                individual.Parameters["ShortBlockStrengthThreshold"] = _random.Next(40, 81);
                individual.Parameters["ShortVolumeReduction"] = _random.Next(30, 71);

                // === ДОПОЛНИТЕЛЬНЫЕ ФИЛЬТРЫ ===
                individual.Parameters["MaxVolatilityForShorts"] = (decimal)(_random.NextDouble() * 8 + 2);

                // === ЗАДЕРЖКИ ДЛЯ ШОРТОВ ===
                individual.Parameters["ShortDelayMultiplierUptrend"] = (decimal)(_random.NextDouble() * 4 + 1);
                individual.Parameters["ShortDelayMultiplierStrongUptrend"] = (decimal)(_random.NextDouble() * 6.5 + 1.5);

                // === ЗАЩИТНЫЕ МЕХАНИЗМЫ (объединены параметры просадки) ===
                individual.Parameters["MaxDrawdownPerInstrument"] = (decimal)(_random.NextDouble() * 9 + 1);
                individual.Parameters["VolumeReductionFactor"] = (decimal)(_random.NextDouble() * 0.6 + 0.2);

                // === ЗАЩИТА ОТ ПАМПА/ДАМПА ===
                individual.Parameters["PumpDetectionSensitivity"] = (decimal)(_random.NextDouble() * 3.5 + 1.5);
                individual.Parameters["DumpDetectionSensitivity"] = (decimal)(_random.NextDouble() * -3.5 - 1.5);
                individual.Parameters["MinVolumeSpikeRatio"] = (decimal)(_random.NextDouble() * 8 + 2);
                individual.Parameters["EmergencyVolumeReduction"] = (decimal)(_random.NextDouble() * 0.7 + 0.1);



                // === СИСТЕМА ЗАДЕРЖЕК (убраны случайные задержки) ===
                individual.Parameters["DelayBeforeOpenSeconds"] = _random.Next(0, 31);
                individual.Parameters["DelayBetweenOrdersSeconds"] = _random.Next(0, 16);

                return individual;
            }

            /// <summary>
            /// Запуск эволюции на указанное количество поколений
            /// </summary>
            public async Task<Individual> EvolveAsync(int generations, decimal mutationRate, decimal crossoverRate)
            {
                _robot.SendNewLogMessage($"Запуск эволюции на {generations} поколений...", LogMessageType.System);

                for (int generation = 0; generation < generations; generation++)
                {
                    await EvaluatePopulationAsync();
                    
                    // Селекция и создание нового поколения
                    var newPopulation = new List<Individual>();
                    
                    // Элитизм - сохраняем лучших
                    var eliteCount = (int)(_population.Count * 0.1);
                    var elite = _population.OrderByDescending(ind => ind.Fitness).Take(eliteCount).ToList();
                    newPopulation.AddRange(elite);

                    // Заполняем остальную часть популяции через кроссовер и мутацию
                    while (newPopulation.Count < _population.Count)
                    {
                        Individual parent1 = SelectParent();
                        Individual parent2 = SelectParent();

                        if (_random.NextDouble() < (double)crossoverRate)
                        {
                            var offspring = Crossover(parent1, parent2);
                            newPopulation.Add(offspring);
                        }
                        else
                        {
                            newPopulation.Add(parent1.Clone());
                        }
                    }

                    // Мутация
                    foreach (var individual in newPopulation.Skip(eliteCount))
                    {
                        if (_random.NextDouble() < (double)mutationRate)
                        {
                            Mutate(individual);
                        }
                    }

                    _population = newPopulation;

                    // Обновляем лучшую особь
                    var currentBest = _population.OrderByDescending(ind => ind.Fitness).First();
                    if (currentBest.Fitness > _bestIndividual.Fitness)
                    {
                        _bestIndividual = currentBest.Clone();
                    }

                    if (generation % 10 == 0)
                    {
                        _robot.SendNewLogMessage($"Поколение {generation}: Лучшая приспособленность = {_bestIndividual.Fitness:F4}, " +
                                               $"Прибыль = {_bestIndividual.TotalProfit:F2}%, " +
                                               $"Шарп = {_bestIndividual.SharpeRatio:F2}", 
                                               LogMessageType.System);
                    }
                }

                _robot.SendNewLogMessage($"Эволюция завершена. Лучшая приспособленность: {_bestIndividual.Fitness:F4}", LogMessageType.System);
                return _bestIndividual;
            }

            /// <summary>
            /// Оценка приспособленности популяции
            /// </summary>
            private async Task EvaluatePopulationAsync()
            {
                var tasks = _population.Select(individual => 
                    Task.Run(() => EvaluateFitness(individual))).ToArray();
                
                await Task.WhenAll(tasks);
            }

            /// <summary>
            /// Оценка приспособленности особи
            /// </summary>
            private void EvaluateFitness(Individual individual)
            {
                try
                {
                    // Симуляция торговли с данными параметрами на исторических данных
                    var simulationResult = SimulateTrading(individual);

                    individual.TotalProfit = (double)simulationResult.TotalProfit;
                    individual.SharpeRatio = simulationResult.SharpeRatio;
                    individual.MaxDrawdown = (double)simulationResult.MaxDrawdown;
                    individual.WinRate = simulationResult.WinRate;
                    individual.TradeCount = simulationResult.TradeCount;
                    individual.ProfitFactor = simulationResult.ProfitFactor;

                    // Расчет комплексной приспособленности
                    individual.Fitness = CalculateFitnessScore(individual, _robot.GeneticFitnessFunction.ValueString);
                }
                catch (Exception ex)
                {
                    _robot.SendNewLogMessage($"Ошибка оценки приспособленности: {ex.Message}", LogMessageType.Error);
                    individual.Fitness = -1000;
                }
            }

            /// <summary>
            /// Расчет оценки приспособленности
            /// </summary>
            private double CalculateFitnessScore(Individual individual, string fitnessFunction)
            {
                switch (fitnessFunction)
                {
                    case "TotalProfit":
                        return individual.TotalProfit * (1 - individual.MaxDrawdown / 100);
                    
                    case "SharpeRatio":
                        return individual.SharpeRatio * Math.Sqrt(Math.Max(individual.TradeCount, 1));
                    
                    case "ProfitFactor":
                        return individual.ProfitFactor * (1 - individual.MaxDrawdown / 100) * Math.Sqrt(individual.TradeCount);
                    
                    case "WinRate":
                        return individual.WinRate * individual.TotalProfit / 100;
                    
                    case "CustomComposite":
                        // Композитная метрика с весами
                        return individual.TotalProfit * 0.4 + 
                               individual.SharpeRatio * 0.3 + 
                               (100 - individual.MaxDrawdown) * 0.2 + 
                               individual.WinRate * 0.1;
                    
                    default:
                        return individual.TotalProfit * (1 - individual.MaxDrawdown / 100);
                }
            }

            /// <summary>
            /// Симуляция торговли для оценки параметров (исправленный метод)
            /// </summary>
            private SimulationResult SimulateTrading(Individual individual)
            {
                try
                {
                    var tabs = _robot.TabScreener.Tabs.ToList();
                    if (!tabs.Any()) return new SimulationResult();

                    var tab = tabs.First() as BotTabSimple;
                    if (tab == null) return new SimulationResult();

                    var candles = _robot.GetHistoricalDataForBacktest(tab);
                    if (candles == null || candles.Count < 50) return new SimulationResult();

                    // Фильтруем данные по периоду
                    var filteredCandles = _robot.FilterCandlesByPeriod(candles, 90); // 90 дней для оптимизации
                    if (filteredCandles.Count < 30) return new SimulationResult();

                    // Создаем временную копию параметров для симуляции
                    var originalParams = _robot.GetOptimizationParameters();

                    // Применяем параметры индивидуума
                    ApplyIndividualParametersForSimulation(individual);

                    // Запускаем бэктест с трендовыми условиями
                    var backtestResult = _robot.RunBacktestOnCandles(tab, filteredCandles);

                    // Восстанавливаем оригинальные параметры
                    _robot.RestoreOptimizationParameters(originalParams);

                    return new SimulationResult
                    {
                        TotalProfit = backtestResult?.TotalProfit ?? -1000,
                        SharpeRatio = backtestResult != null && backtestResult.TotalTrades > 0
                            ? (backtestResult.TotalProfit / Math.Max(backtestResult.MaxDrawdown, 1)) * 0.1
                            : -10,
                        MaxDrawdown = backtestResult?.MaxDrawdown ?? 100,
                        WinRate = backtestResult?.WinRate ?? 0,
                        TradeCount = backtestResult?.TotalTrades ?? 0,
                        ProfitFactor = backtestResult?.ProfitFactor ?? 0
                    };
                }
                catch (Exception ex)
                {
                    _robot.SendNewLogMessage($"Ошибка симуляции торговли: {ex.Message}", LogMessageType.Error);
                    return new SimulationResult
                    {
                        TotalProfit = -1000,
                        SharpeRatio = -10,
                        MaxDrawdown = 100,
                        WinRate = 0,
                        TradeCount = 0,
                        ProfitFactor = 0
                    };
                }
            }

            /// <summary>
            /// Применение параметров индивидуума для симуляции
            /// </summary>
            private void ApplyIndividualParametersForSimulation(Individual individual)
            {
                try
                {
                    if (individual.Parameters.ContainsKey("VolumeReductionPerOrder"))
                        _robot.VolumeReductionPerOrder.ValueDecimal = individual.Parameters["VolumeReductionPerOrder"];
                    if (individual.Parameters.ContainsKey("MaxOrdersCount"))
                        _robot.MaxOrdersCount.ValueInt = (int)individual.Parameters["MaxOrdersCount"];
                    if (individual.Parameters.ContainsKey("IchimokuTenkanPeriod"))
                        _robot.IchimokuTenkanPeriod.ValueInt = (int)individual.Parameters["IchimokuTenkanPeriod"];
                    if (individual.Parameters.ContainsKey("IchimokuKijunPeriod"))
                        _robot.IchimokuKijunPeriod.ValueInt = (int)individual.Parameters["IchimokuKijunPeriod"];
                    if (individual.Parameters.ContainsKey("IchimokuSenkouBPeriod"))
                        _robot.IchimokuSenkouBPeriod.ValueInt = (int)individual.Parameters["IchimokuSenkouBPeriod"];
                    if (individual.Parameters.ContainsKey("IchimokuDisplacement"))
                        _robot.IchimokuDisplacement.ValueInt = (int)individual.Parameters["IchimokuDisplacement"];
                    if (individual.Parameters.ContainsKey("AtrPeriod"))
                        _robot.AtrPeriod.ValueInt = (int)individual.Parameters["AtrPeriod"];
                    if (individual.Parameters.ContainsKey("BaseDistanceAtrMultiplier"))
                        _robot.BaseDistanceAtrMultiplier.ValueDecimal = individual.Parameters["BaseDistanceAtrMultiplier"];
                    if (individual.Parameters.ContainsKey("RsiPeriod"))
                        _robot.RsiPeriod.ValueInt = (int)individual.Parameters["RsiPeriod"];
                    if (individual.Parameters.ContainsKey("RsiOverbought"))
                        _robot.RsiOverbought.ValueDecimal = individual.Parameters["RsiOverbought"];
                    if (individual.Parameters.ContainsKey("RsiOversold"))
                        _robot.RsiOversold.ValueDecimal = individual.Parameters["RsiOversold"];
                    if (individual.Parameters.ContainsKey("ShortBlockStrengthThreshold"))
                        _robot.ShortBlockStrengthThreshold1.ValueDecimal = individual.Parameters["ShortBlockStrengthThreshold"];
                    if (individual.Parameters.ContainsKey("ShortVolumeReduction"))
                        _robot.ShortVolumeReduction1.ValueDecimal = individual.Parameters["ShortVolumeReduction"];
                    if (individual.Parameters.ContainsKey("MaxVolatilityForShorts"))
                        _robot.MaxVolatilityForShorts.ValueDecimal = individual.Parameters["MaxVolatilityForShorts"];
                    if (individual.Parameters.ContainsKey("ShortDelayMultiplierUptrend"))
                        _robot.ShortDelayMultiplierUptrend.ValueDecimal = individual.Parameters["ShortDelayMultiplierUptrend"];
                    if (individual.Parameters.ContainsKey("ShortDelayMultiplierStrongUptrend"))
                        _robot.ShortDelayMultiplierStrongUptrend.ValueDecimal = individual.Parameters["ShortDelayMultiplierStrongUptrend"];
                    if (individual.Parameters.ContainsKey("DelayBeforeOpenSeconds"))
                        _robot.DelayBeforeOpenSeconds.ValueInt = (int)individual.Parameters["DelayBeforeOpenSeconds"];
                    if (individual.Parameters.ContainsKey("DelayBetweenOrdersSeconds"))
                        _robot.DelayBetweenOrdersSeconds.ValueInt = (int)individual.Parameters["DelayBetweenOrdersSeconds"];
                }
                catch (Exception ex)
                {
                    _robot.SendNewLogMessage($"Ошибка применения параметров для симуляции: {ex.Message}", LogMessageType.Error);
                }
            }

            /// <summary>
            /// Восстановление оригинальных параметров
            /// </summary>
            private void RestoreOriginalParameters(Dictionary<string, decimal> originalParams)
            {
                try
                {
                    foreach (var param in originalParams)
                    {
                        switch (param.Key)
                        {
                            case "VolumeReductionPerOrder":
                                _robot.VolumeReductionPerOrder.ValueDecimal = param.Value;
                                break;
                            case "MaxOrdersCount":
                                _robot.MaxOrdersCount.ValueInt = (int)param.Value;
                                break;
                            case "IchimokuTenkanPeriod":
                                _robot.IchimokuTenkanPeriod.ValueInt = (int)param.Value;
                                break;
                            case "IchimokuKijunPeriod":
                                _robot.IchimokuKijunPeriod.ValueInt = (int)param.Value;
                                break;
                            case "IchimokuSenkouBPeriod":
                                _robot.IchimokuSenkouBPeriod.ValueInt = (int)param.Value;
                                break;
                            case "IchimokuDisplacement":
                                _robot.IchimokuDisplacement.ValueInt = (int)param.Value;
                                break;
                            case "AtrPeriod":
                                _robot.AtrPeriod.ValueInt = (int)param.Value;
                                break;
                            case "BaseDistanceAtrMultiplier":
                                _robot.BaseDistanceAtrMultiplier.ValueDecimal = param.Value;
                                break;
                            case "RsiPeriod":
                                _robot.RsiPeriod.ValueInt = (int)param.Value;
                                break;
                            case "RsiOverbought":
                                _robot.RsiOverbought.ValueDecimal = param.Value;
                                break;
                            case "RsiOversold":
                                _robot.RsiOversold.ValueDecimal = param.Value;
                                break;

                            case "MaxVolatilityForShorts":
                                _robot.MaxVolatilityForShorts.ValueDecimal = param.Value;
                                break;
                            case "ShortDelayMultiplierUptrend":
                                _robot.ShortDelayMultiplierUptrend.ValueDecimal = param.Value;
                                break;
                            case "ShortDelayMultiplierStrongUptrend":
                                _robot.ShortDelayMultiplierStrongUptrend.ValueDecimal = param.Value;
                                break;
                            case "DelayBeforeOpenSeconds":
                                _robot.DelayBeforeOpenSeconds.ValueInt = (int)param.Value;
                                break;
            case "DelayBetweenOrdersSeconds":
                _robot.DelayBetweenOrdersSeconds.ValueInt = (int)param.Value;
                break;
            default:
                _robot.SendNewLogMessage($"Неизвестный параметр оптимизации: {param.Key}", LogMessageType.Error);
                break;
        }
                    }
                }
                catch (Exception ex)
                {
                    _robot.SendNewLogMessage($"Ошибка восстановления параметров: {ex.Message}", LogMessageType.Error);
                }
            }

            /// <summary>
            /// Селекция родителя методом рулетки
            /// </summary>
            private Individual SelectParent()
            {
                double totalFitness = _population.Sum(ind => Math.Max(ind.Fitness, 0));
                double randomValue = _random.NextDouble() * totalFitness;

                double currentSum = 0;
                foreach (var individual in _population)
                {
                    currentSum += Math.Max(individual.Fitness, 0);
                    if (currentSum >= randomValue)
                    {
                        return individual;
                    }
                }

                return _population.Last();
            }

            /// <summary>
            /// Кроссовер двух родителей
            /// </summary>
            private Individual Crossover(Individual parent1, Individual parent2)
            {
                var offspring = new Individual();
                var crossoverPoint = _random.Next(parent1.Parameters.Count);

                int i = 0;
                foreach (var param in parent1.Parameters)
                {
                    if (i < crossoverPoint)
                    {
                        offspring.Parameters[param.Key] = param.Value;
                    }
                    else
                    {
                        offspring.Parameters[param.Key] = parent2.Parameters[param.Key];
                    }
                    i++;
                }

                return offspring;
            }

            /// <summary>
            /// Мутация особи
            /// </summary>
            private void Mutate(Individual individual)
            {
                var paramToMutate = individual.Parameters.Keys.ElementAt(_random.Next(individual.Parameters.Count));
                
                decimal currentValue = individual.Parameters[paramToMutate];
                decimal mutation = (decimal)((_random.NextDouble() - 0.5) * 0.2);
                
                individual.Parameters[paramToMutate] = Math.Max(0.01m, currentValue + mutation * currentValue);
            }
        }

        /// <summary>
        /// Результат симуляции торговли
        /// </summary>
        public class SimulationResult
        {
            public double TotalProfit { get; set; }
            public double SharpeRatio { get; set; }
            public double MaxDrawdown { get; set; }
            public double WinRate { get; set; }
            public int TradeCount { get; set; }
            public double ProfitFactor { get; set; }
        }

        #endregion

        #region МЕТОДЫ ГЕНЕТИЧЕСКОЙ ОПТИМИЗАЦИИ

        /// <summary>
        /// Запуск генетической оптимизации
        /// </summary>
        public async void StartGeneticOptimization()
        {
            if (_isOptimizationRunning)
            {
                SendNewLogMessage("Оптимизация уже выполняется", LogMessageType.System);
                return;
            }

            _isOptimizationRunning = true;

            try
            {
                SendNewLogMessage("🚀 Запуск генетической оптимизации...", LogMessageType.System);

                _geneticAlgorithm.InitializePopulation(GeneticPopulationSize.ValueInt);

                var bestIndividual = await _geneticAlgorithm.EvolveAsync(
                    GeneticGenerations.ValueInt,
                    GeneticMutationRate.ValueDecimal,
                    GeneticCrossoverRate.ValueDecimal);

                // Применяем лучшие параметры
                ApplyOptimizedParameters(bestIndividual);

                _optimizedParameters = bestIndividual.Parameters;
                _lastOptimizationTime = DateTime.Now;

                SendNewLogMessage($"✅ Генетическая оптимизация завершена. Лучшие параметры применены.", LogMessageType.System);
                LogOptimizationResults(bestIndividual);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка генетической оптимизации: {ex.Message}", LogMessageType.Error);
            }
            finally
            {
                _isOptimizationRunning = false;
            }
        }

        /// <summary>
        /// Запуск бэктестинга на исторических данных
        /// </summary>
        public void StartBacktesting()
        {
            try
            {
                SendNewLogMessage("📊 Запуск бэктестинга на исторических данных...", LogMessageType.System);

                var tabs = TabScreener.Tabs.ToList();
                if (!tabs.Any())
                {
                    SendNewLogMessage("❌ Нет доступных инструментов для бэктестинга", LogMessageType.Error);
                    return;
                }

                Dictionary<string, BacktestResult> backtestResults = new Dictionary<string, BacktestResult>();

                foreach (var tab in tabs)
                {
                    if (!(tab is BotTabSimple botTab)) continue;

                    string security = botTab.Security.Name;

                    // Получаем исторические данные в зависимости от выбранного источника
                    var candles = GetHistoricalDataForBacktest(botTab);

                    if (candles == null || candles.Count < BacktestMinTrades.ValueInt * 2)
                    {
                        SendNewLogMessage($"⚠️ Недостаточно данных для бэктестинга {security}: {candles?.Count ?? 0} свечей", LogMessageType.System);
                        continue;
                    }

                    // Фильтруем данные по периоду бэктеста
                    var filteredCandles = FilterCandlesByPeriod(candles, BacktestPeriodDays.ValueInt);
                    if (filteredCandles.Count < BacktestMinTrades.ValueInt * 2)
                    {
                        SendNewLogMessage($"⚠️ Недостаточно данных после фильтрации периода для {security}", LogMessageType.System);
                        continue;
                    }

                    SendNewLogMessage($"📈 Бэктестинг {security}: {filteredCandles.Count} свечей за период ({BacktestDataSource.ValueString})", LogMessageType.System);

                    var result = RunBacktestOnCandles(botTab, filteredCandles);
                    if (result != null && result.TotalTrades >= BacktestMinTrades.ValueInt)
                    {
                        backtestResults[security] = result;
                        SendNewLogMessage($"✅ Результат бэктеста {security}: Прибыль {result.TotalProfit:F2}%, Просадка {result.MaxDrawdown:F2}%, Сделок {result.TotalTrades}", LogMessageType.System);
                    }
                }

                if (backtestResults.Any() && ApplyBacktestResults.ValueBool)
                {
                    ApplyBestBacktestResults(backtestResults);
                }

                SendNewLogMessage("✅ Бэктестинг завершен", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка бэктестинга: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Фильтрация свечей по периоду бэктеста
        /// </summary>
        private List<Candle> FilterCandlesByPeriod(List<Candle> candles, int days)
        {
            if (candles == null || candles.Count == 0 || days <= 0)
                return new List<Candle>();

            DateTime endDate = DateTime.Now;
            DateTime startDate = endDate.AddDays(-days);

            return candles.Where(c => c.TimeStart >= startDate && c.TimeStart <= endDate)
                         .OrderBy(c => c.TimeStart)
                         .ToList();
        }

        /// <summary>
        /// Запуск бэктеста на отфильтрованных свечах
        /// </summary>
        private BacktestResult RunBacktestOnCandles(BotTabSimple tab, List<Candle> candles)
        {
            try
            {
                var result = new BacktestResult();
                var positions = new List<BacktestPosition>();
                decimal currentEquity = 10000m; // Начальный капитал
                decimal peakEquity = currentEquity;
                decimal maxDrawdown = 0;

                int step = Math.Max(1, BacktestStepMinutes.ValueInt);

                for (int i = 0; i < candles.Count; i += step)
                {
                    var currentCandle = candles[i];

                    // Обновляем анализ тренда для текущей свечи
                    UpdateTrendAnalysisForBacktest(tab.Security.Name, tab, currentCandle);

                    // Проверяем условия открытия позиций
                    CheckBacktestLongConditions(tab, currentCandle, positions, ref currentEquity);
                    CheckBacktestShortConditions(tab, currentCandle, positions, ref currentEquity);

                    // Проверяем условия закрытия позиций
                    CheckBacktestExitConditions(positions, currentCandle, ref currentEquity);

                    // Обновляем максимальную просадку
                    if (currentEquity > peakEquity)
                        peakEquity = currentEquity;

                    decimal drawdown = (peakEquity - currentEquity) / peakEquity * 100;
                    if (drawdown > maxDrawdown)
                        maxDrawdown = drawdown;
                }

                // Закрываем все оставшиеся позиции по последней цене
                if (positions.Any() && candles.Any())
                {
                    var lastCandle = candles.Last();
                    foreach (var position in positions.ToList())
                    {
                        CloseBacktestPosition(position, lastCandle.Close, ref currentEquity);
                        result.TotalTrades++;
                    }
                }

                result.TotalProfit = (double)((currentEquity - 10000m) / 10000m * 100);
                result.MaxDrawdown = (double)maxDrawdown;
                result.TotalTrades = positions.Count;
                result.WinTrades = positions.Count(p => p.Profit > 0);
                result.LossTrades = positions.Count(p => p.Profit < 0);

                if (result.TotalTrades > 0)
                {
                    result.WinRate = (double)result.WinTrades / result.TotalTrades * 100;
                    result.ProfitFactor = (double)(positions.Sum(p => Math.Max(p.Profit, 0)) /
                                        Math.Max(positions.Sum(p => Math.Min(p.Profit, 0)), 0.01m));
                }

                return result;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка бэктеста: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// Применение лучших результатов бэктеста
        /// </summary>
        private void ApplyBestBacktestResults(Dictionary<string, BacktestResult> results)
        {
            try
            {
                var bestResult = results.OrderByDescending(r => r.Value.TotalProfit)
                                       .ThenBy(r => r.Value.MaxDrawdown)
                                       .FirstOrDefault();

                if (bestResult.Value != null)
                {
                    SendNewLogMessage($"🎯 Лучший результат бэктеста: {bestResult.Key} - Прибыль {bestResult.Value.TotalProfit:F2}%, Просадка {bestResult.Value.MaxDrawdown:F2}%", LogMessageType.System);

                    // Здесь можно применить параметры, показавшие лучшие результаты
                    // Пока просто логируем, но в будущем можно сохранять и применять
                    _backtestBestParameters = GetCurrentParameters();
                    _backtestBestResult = bestResult.Value;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка применения результатов бэктеста: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Применение оптимизированных параметров (ВСЕХ)
        /// </summary>
        private void ApplyOptimizedParameters(Individual bestIndividual)
        {
            try
            {
                // === ОСНОВНЫЕ ПАРАМЕТРЫ ===
                if (bestIndividual.Parameters.ContainsKey("VolumeReductionPerOrder"))
                    VolumeReductionPerOrder.ValueDecimal = bestIndividual.Parameters["VolumeReductionPerOrder"];
                if (bestIndividual.Parameters.ContainsKey("MaxOrdersCount"))
                    MaxOrdersCount.ValueInt = (int)bestIndividual.Parameters["MaxOrdersCount"];

                // === ПАРАМЕТРЫ РАССТОЯНИЯ ===
                if (bestIndividual.Parameters.ContainsKey("DistanceBetweenLongOrders"))
                    DistanceBetweenLongOrders.ValueDecimal = bestIndividual.Parameters["DistanceBetweenLongOrders"];
                if (bestIndividual.Parameters.ContainsKey("TakeProfitLong"))
                    TakeProfitLong.ValueDecimal = bestIndividual.Parameters["TakeProfitLong"];
                if (bestIndividual.Parameters.ContainsKey("DistanceBetweenShortOrders"))
                    DistanceBetweenShortOrders.ValueDecimal = bestIndividual.Parameters["DistanceBetweenShortOrders"];
                if (bestIndividual.Parameters.ContainsKey("TakeProfitShort"))
                    TakeProfitShort.ValueDecimal = bestIndividual.Parameters["TakeProfitShort"];

                // === ИНДИКАТОР ИШИМОКУ (ЗАМЕНА EMA) ===
                if (bestIndividual.Parameters.ContainsKey("IchimokuTenkanPeriod"))
                    IchimokuTenkanPeriod.ValueInt = (int)bestIndividual.Parameters["IchimokuTenkanPeriod"];
                if (bestIndividual.Parameters.ContainsKey("IchimokuKijunPeriod"))
                    IchimokuKijunPeriod.ValueInt = (int)bestIndividual.Parameters["IchimokuKijunPeriod"];
                if (bestIndividual.Parameters.ContainsKey("IchimokuSenkouBPeriod"))
                    IchimokuSenkouBPeriod.ValueInt = (int)bestIndividual.Parameters["IchimokuSenkouBPeriod"];
                if (bestIndividual.Parameters.ContainsKey("IchimokuDisplacement"))
                    IchimokuDisplacement.ValueInt = (int)bestIndividual.Parameters["IchimokuDisplacement"];
                
                if (bestIndividual.Parameters.ContainsKey("AtrPeriod"))
                    AtrPeriod.ValueInt = (int)bestIndividual.Parameters["AtrPeriod"];
                if (bestIndividual.Parameters.ContainsKey("BaseDistanceAtrMultiplier"))
                    BaseDistanceAtrMultiplier.ValueDecimal = bestIndividual.Parameters["BaseDistanceAtrMultiplier"];
                if (bestIndividual.Parameters.ContainsKey("RsiPeriod"))
                    RsiPeriod.ValueInt = (int)bestIndividual.Parameters["RsiPeriod"];
                if (bestIndividual.Parameters.ContainsKey("RsiOverbought"))
                    RsiOverbought.ValueDecimal = bestIndividual.Parameters["RsiOverbought"];
                if (bestIndividual.Parameters.ContainsKey("RsiOversold"))
                    RsiOversold.ValueDecimal = bestIndividual.Parameters["RsiOversold"];


                // === ГРАДУИРОВАННАЯ БЛОКИРОВКА ШОРТОВ (упрощённая версия - 2 параметра вместо 6) ===
                if (bestIndividual.Parameters.ContainsKey("ShortBlockStrengthThreshold"))
                {
                    decimal baseThreshold = bestIndividual.Parameters["ShortBlockStrengthThreshold"];
                    ShortBlockStrengthThreshold1.ValueDecimal = baseThreshold;
                    ShortBlockStrengthThreshold2.ValueDecimal = baseThreshold + 10;
                    ShortBlockStrengthThreshold3.ValueDecimal = baseThreshold + 20;
                }
                if (bestIndividual.Parameters.ContainsKey("ShortVolumeReduction"))
                {
                    decimal baseReduction = bestIndividual.Parameters["ShortVolumeReduction"];
                    ShortVolumeReduction1.ValueDecimal = baseReduction;
                    ShortVolumeReduction2.ValueDecimal = Math.Min(100, baseReduction + 10);
                    ShortVolumeReduction3.ValueDecimal = Math.Min(100, baseReduction + 20);
                }

                // === ДОПОЛНИТЕЛЬНЫЕ ФИЛЬТРЫ ===
                if (bestIndividual.Parameters.ContainsKey("MaxVolatilityForShorts"))
                    MaxVolatilityForShorts.ValueDecimal = bestIndividual.Parameters["MaxVolatilityForShorts"];

                // === ЗАДЕРЖКИ ДЛЯ ШОРТОВ ===
                if (bestIndividual.Parameters.ContainsKey("ShortDelayMultiplierUptrend"))
                    ShortDelayMultiplierUptrend.ValueDecimal = bestIndividual.Parameters["ShortDelayMultiplierUptrend"];
                if (bestIndividual.Parameters.ContainsKey("ShortDelayMultiplierStrongUptrend"))
                    ShortDelayMultiplierStrongUptrend.ValueDecimal = bestIndividual.Parameters["ShortDelayMultiplierStrongUptrend"];

                // === ЗАЩИТНЫЕ МЕХАНИЗМЫ ===
                if (bestIndividual.Parameters.ContainsKey("MaxDrawdownPerSecurity"))
                    MaxDrawdownPerSecurity.ValueDecimal = bestIndividual.Parameters["MaxDrawdownPerSecurity"];
                if (bestIndividual.Parameters.ContainsKey("MaxDrawdownPerInstrument"))
                    MaxDrawdownPerInstrument.ValueDecimal = bestIndividual.Parameters["MaxDrawdownPerInstrument"];
                if (bestIndividual.Parameters.ContainsKey("VolumeReductionFactor"))
                    VolumeReductionFactor.ValueDecimal = bestIndividual.Parameters["VolumeReductionFactor"];

                // === ЗАЩИТА ОТ ПАМПА/ДАМПА ===
                if (bestIndividual.Parameters.ContainsKey("PumpDetectionSensitivity"))
                    PumpDetectionSensitivity.ValueDecimal = bestIndividual.Parameters["PumpDetectionSensitivity"];
                if (bestIndividual.Parameters.ContainsKey("DumpDetectionSensitivity"))
                    DumpDetectionSensitivity.ValueDecimal = bestIndividual.Parameters["DumpDetectionSensitivity"];
                if (bestIndividual.Parameters.ContainsKey("MinVolumeSpikeRatio"))
                    MinVolumeSpikeRatio.ValueDecimal = bestIndividual.Parameters["MinVolumeSpikeRatio"];
                if (bestIndividual.Parameters.ContainsKey("EmergencyVolumeReduction"))
                    EmergencyVolumeReduction.ValueDecimal = bestIndividual.Parameters["EmergencyVolumeReduction"];


                if (bestIndividual.Parameters.ContainsKey("DelayBeforeOpenSeconds"))
                    DelayBeforeOpenSeconds.ValueInt = (int)bestIndividual.Parameters["DelayBeforeOpenSeconds"];
                if (bestIndividual.Parameters.ContainsKey("DelayBetweenOrdersSeconds"))
                    DelayBetweenOrdersSeconds.ValueInt = (int)bestIndividual.Parameters["DelayBetweenOrdersSeconds"];
                if (bestIndividual.Parameters.ContainsKey("RandomDelayRange"))
                    RandomDelayRange.ValueInt = (int)bestIndividual.Parameters["RandomDelayRange"];

                SendNewLogMessage("✅ Все оптимизированные параметры применены", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка применения параметров: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Логирование результатов оптимизации
        /// </summary>
        private void LogOptimizationResults(Individual bestIndividual)
        {
            var message = $"📊 РЕЗУЛЬТАТЫ ОПТИМИЗАЦИИ:\n" +
                         $"Приспособленность: {bestIndividual.Fitness:F4}\n" +
                         $"Общая прибыль: {bestIndividual.TotalProfit:F2}%\n" +
                         $"Коэффициент Шарпа: {bestIndividual.SharpeRatio:F2}\n" +
                         $"Макс. просадка: {bestIndividual.MaxDrawdown:F2}%\n" +
                         $"Винрейт: {bestIndividual.WinRate:F1}%\n" +
                         $"Количество сделок: {bestIndividual.TradeCount}\n" +
                         $"Профит-фактор: {bestIndividual.ProfitFactor:F2}\n\n" +
                         $"ЛУЧШИЕ ПАРАМЕТРЫ:\n";

            foreach (var param in bestIndividual.Parameters.Take(15))
            {
                message += $"{param.Key}: {param.Value:F4}\n";
            }

            SendNewLogMessage(message, LogMessageType.System);
        }

        /// <summary>
        /// Проверка необходимости реал-тайм оптимизации
        /// </summary>
        private void CheckRealTimeOptimization()
        {
            if (!UseRealTimeGeneticOptimization.ValueBool) return;

            var daysSinceLastOptimization = (DateTime.Now - _lastOptimizationTime).TotalDays;
            
            if (daysSinceLastOptimization >= GeneticOptimizationInterval.ValueInt)
            {
                SendNewLogMessage($"🔄 Запуск реал-тайм оптимизации (прошло {daysSinceLastOptimization:F1} дней)", 
                                LogMessageType.System);
                StartGeneticOptimization();
            }
        }

        /// <summary>
        /// Получение параметров для оптимизации (только оптимизируемые параметры)
        /// </summary>
        private Dictionary<string, decimal> GetOptimizationParameters()
        {
            return new Dictionary<string, decimal>
            {
                ["VolumeReductionPerOrder"] = VolumeReductionPerOrder.ValueDecimal,
                ["MaxOrdersCount"] = MaxOrdersCount.ValueInt,
                ["IchimokuTenkanPeriod"] = IchimokuTenkanPeriod.ValueInt,
                ["IchimokuKijunPeriod"] = IchimokuKijunPeriod.ValueInt,
                ["IchimokuSenkouBPeriod"] = IchimokuSenkouBPeriod.ValueInt,
                ["IchimokuDisplacement"] = IchimokuDisplacement.ValueInt,
                ["AtrPeriod"] = AtrPeriod.ValueInt,
                ["BaseDistanceAtrMultiplier"] = BaseDistanceAtrMultiplier.ValueDecimal,
                ["RsiPeriod"] = RsiPeriod.ValueInt,
                ["RsiOverbought"] = RsiOverbought.ValueDecimal,
                ["RsiOversold"] = RsiOversold.ValueDecimal,
                ["MaxVolatilityForShorts"] = MaxVolatilityForShorts.ValueDecimal,
                ["ShortDelayMultiplierUptrend"] = ShortDelayMultiplierUptrend.ValueDecimal,
                ["ShortDelayMultiplierStrongUptrend"] = ShortDelayMultiplierStrongUptrend.ValueDecimal,
                ["DelayBeforeOpenSeconds"] = DelayBeforeOpenSeconds.ValueInt,
                ["DelayBetweenOrdersSeconds"] = DelayBetweenOrdersSeconds.ValueInt
            };
        }

        /// <summary>
        /// Восстановление параметров оптимизации
        /// </summary>
        private void RestoreOptimizationParameters(Dictionary<string, decimal> originalParams)
        {
            try
            {
                foreach (var param in originalParams)
                {
                    switch (param.Key)
                    {
                        case "VolumeReductionPerOrder":
                            VolumeReductionPerOrder.ValueDecimal = param.Value;
                            break;
                        case "MaxOrdersCount":
                            MaxOrdersCount.ValueInt = (int)param.Value;
                            break;
                        case "IchimokuTenkanPeriod":
                            IchimokuTenkanPeriod.ValueInt = (int)param.Value;
                            break;
                        case "IchimokuKijunPeriod":
                            IchimokuKijunPeriod.ValueInt = (int)param.Value;
                            break;
                        case "IchimokuSenkouBPeriod":
                            IchimokuSenkouBPeriod.ValueInt = (int)param.Value;
                            break;
                        case "IchimokuDisplacement":
                            IchimokuDisplacement.ValueInt = (int)param.Value;
                            break;
                        case "AtrPeriod":
                            AtrPeriod.ValueInt = (int)param.Value;
                            break;
                        case "BaseDistanceAtrMultiplier":
                            BaseDistanceAtrMultiplier.ValueDecimal = param.Value;
                            break;
                        case "RsiPeriod":
                            RsiPeriod.ValueInt = (int)param.Value;
                            break;
                        case "RsiOverbought":
                            RsiOverbought.ValueDecimal = param.Value;
                            break;
                        case "RsiOversold":
                            RsiOversold.ValueDecimal = param.Value;
                            break;
                        case "MaxVolatilityForShorts":
                            MaxVolatilityForShorts.ValueDecimal = param.Value;
                            break;
                        case "ShortDelayMultiplierUptrend":
                            ShortDelayMultiplierUptrend.ValueDecimal = param.Value;
                            break;
                        case "ShortDelayMultiplierStrongUptrend":
                            ShortDelayMultiplierStrongUptrend.ValueDecimal = param.Value;
                            break;
                        case "DelayBeforeOpenSeconds":
                            DelayBeforeOpenSeconds.ValueInt = (int)param.Value;
                            break;
                        case "DelayBetweenOrdersSeconds":
                            DelayBetweenOrdersSeconds.ValueInt = (int)param.Value;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка восстановления параметров оптимизации: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Получение текущих параметров для логирования
        /// </summary>
        public Dictionary<string, decimal> GetCurrentParameters()
        {
            return new Dictionary<string, decimal>
            {
                ["VolumeReductionPerOrder"] = VolumeReductionPerOrder.ValueDecimal,
                ["MaxOrdersCount"] = MaxOrdersCount.ValueInt,
                ["DistanceBetweenLongOrders"] = DistanceBetweenLongOrders.ValueDecimal,
                ["TakeProfitLong"] = TakeProfitLong.ValueDecimal,
                ["DistanceBetweenShortOrders"] = DistanceBetweenShortOrders.ValueDecimal,
                ["TakeProfitShort"] = TakeProfitShort.ValueDecimal,
                ["IchimokuTenkanPeriod"] = IchimokuTenkanPeriod.ValueInt,
                ["IchimokuKijunPeriod"] = IchimokuKijunPeriod.ValueInt,
                ["IchimokuSenkouBPeriod"] = IchimokuSenkouBPeriod.ValueInt,
                ["IchimokuDisplacement"] = IchimokuDisplacement.ValueInt,
                ["RsiPeriod"] = RsiPeriod.ValueInt,
                ["RsiOverbought"] = RsiOverbought.ValueDecimal,
                ["RsiOversold"] = RsiOversold.ValueDecimal
            };
        }

        #endregion

        #region КЛАССЫ БЭКТЕСТИНГА

        /// <summary>
        /// Результат бэктестинга для одного инструмента
        /// </summary>
        public class BacktestResult
        {
            public double TotalProfit { get; set; } // Общая прибыль в процентах
            public double MaxDrawdown { get; set; } // Максимальная просадка в процентах
            public int TotalTrades { get; set; } // Общее количество сделок
            public int WinTrades { get; set; } // Количество прибыльных сделок
            public int LossTrades { get; set; } // Количество убыточных сделок
            public double WinRate { get; set; } // Процент выигрышных сделок
            public double ProfitFactor { get; set; } // Профит-фактор
        }

        /// <summary>
        /// Позиция в бэктесте
        /// </summary>
        public class BacktestPosition
        {
            public string Direction { get; set; } // "Long" или "Short"
            public decimal EntryPrice { get; set; } // Цена входа
            public decimal Volume { get; set; } // Объем
            public decimal Profit { get; set; } // Прибыль/убыток
        }

        #endregion

        #region МЕТОДЫ БЭКТЕСТИНГА

        /// <summary>
        /// Обновление анализа тренда для бэктеста
        /// </summary>
        private void UpdateTrendAnalysisForBacktest(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!_instrumentTrends.ContainsKey(security))
            {
                _instrumentTrends[security] = new TrendAnalysis();
            }

            var trendAnalysis = _instrumentTrends[security];
            trendAnalysis.Update(tab, currentCandle,
                               IchimokuTenkanPeriod.ValueInt,
                               IchimokuKijunPeriod.ValueInt,
                               IchimokuSenkouBPeriod.ValueInt,
                               IchimokuDisplacement.ValueInt,
                               RsiPeriod.ValueInt,
                               AtrPeriod.ValueInt);
        }

        /// <summary>
        /// Проверка условий открытия лонг в бэктесте
        /// </summary>
        private void CheckBacktestLongConditions(BotTabSimple tab, Candle currentCandle,
                                                List<BacktestPosition> positions, ref decimal currentEquity)
        {
            if (!EnableLong.ValueBool) return;

            var trendAnalysis = GetTrendAnalysis(tab.Security.Name);

            // ТРЕНДОВЫЕ УСЛОВИЯ ДЛЯ LONG (совпадают с основными):
            bool ichimokuSignal = trendAnalysis.PriceAboveCloud &&
                                 trendAnalysis.CloudBullish &&
                                 trendAnalysis.TenkanSen > trendAnalysis.KijunSen &&
                                 trendAnalysis.PrimaryTrend == "Up";

            if (!ichimokuSignal)
            {
                // Если нет трендового сигнала Ишимоку - не открываем позицию
                return;
            }

            // ДОПОЛНИТЕЛЬНЫЕ ФИЛЬТРЫ (блокирующие)

            if (UseRsiFilter.ValueBool && trendAnalysis.Rsi > RsiOverbought.ValueDecimal)
                return;

            if (positions.Count >= MaxOrdersCount.ValueInt)
                return;

            decimal currentPrice = currentCandle.Close;

            // Проверка расстояния между ордерами (упрощенная)
            if (positions.Any())
            {
                var lastPosition = positions.Last();
                decimal requiredDistancePercent = DistanceBetweenLongOrders.ValueDecimal;
                decimal requiredDistance = requiredDistancePercent / 100m;

                if (currentPrice > lastPosition.EntryPrice * (1 + requiredDistance))
                    return;
            }

            // Расчет объема
            decimal volume = CalculateBacktestVolume(tab, positions.Count, true);
            if (volume <= 0) return;

            // Открытие позиции
            var position = new BacktestPosition
            {
                Direction = "Long",
                EntryPrice = currentPrice,
                Volume = volume,
                Profit = 0
            };

            positions.Add(position);
        }

        /// <summary>
        /// Проверка условий открытия шорт в бэктесте
        /// </summary>
        private void CheckBacktestShortConditions(BotTabSimple tab, Candle currentCandle,
                                                 List<BacktestPosition> positions, ref decimal currentEquity)
        {
            if (!EnableShort.ValueBool) return;

            var trendAnalysis = GetTrendAnalysis(tab.Security.Name);

            // ТРЕНДОВЫЕ УСЛОВИЯ ДЛЯ SHORT (совпадают с основными):
            bool ichimokuSignal = trendAnalysis.PriceBelowCloud &&
                                 trendAnalysis.CloudBearish &&
                                 trendAnalysis.TenkanSen < trendAnalysis.KijunSen &&
                                 trendAnalysis.PrimaryTrend == "Down";

            if (!ichimokuSignal)
            {
                // Если нет трендового сигнала Ишимоку - не открываем позицию
                return;
            }

            // ДОПОЛНИТЕЛЬНЫЕ ФИЛЬТРЫ (блокирующие)

            if (UseRsiFilter.ValueBool && trendAnalysis.Rsi < RsiOversold.ValueDecimal)
                return;

            if (UseVolatilityFilter.ValueBool && trendAnalysis.VolatilityPercent > MaxVolatilityForShorts.ValueDecimal)
                return;

            var gradedBlockResult = ApplyGradedShortBlocking(tab.Security.Name, trendAnalysis);
            if (!gradedBlockResult.AllowTrading) return;

            if (positions.Count >= MaxOrdersCount.ValueInt)
                return;

            decimal currentPrice = currentCandle.Close;

            // Проверка расстояния между ордерами (упрощенная)
            if (positions.Any())
            {
                var lastPosition = positions.Last();
                decimal requiredDistancePercent = DistanceBetweenShortOrders.ValueDecimal;
                decimal requiredDistance = requiredDistancePercent / 100m;

                if (currentPrice < lastPosition.EntryPrice * (1 - requiredDistance))
                    return;
            }

            // Расчет объема
            decimal volume = CalculateBacktestVolume(tab, positions.Count, false);
            if (volume <= 0) return;

            volume *= gradedBlockResult.VolumeMultiplier;

            // Открытие позиции
            var position = new BacktestPosition
            {
                Direction = "Short",
                EntryPrice = currentPrice,
                Volume = volume,
                Profit = 0
            };

            positions.Add(position);
        }

        /// <summary>
        /// Проверка условий закрытия позиций в бэктесте
        /// </summary>
        private void CheckBacktestExitConditions(List<BacktestPosition> positions, Candle currentCandle, ref decimal currentEquity)
        {
            decimal currentPrice = currentCandle.Close;

            foreach (var position in positions.ToList())
            {
                bool isLong = position.Direction == "Long";
                decimal takeProfitPercent = isLong ? TakeProfitLong.ValueDecimal : TakeProfitShort.ValueDecimal;
                decimal takeProfitMultiplier = takeProfitPercent / 100m;

                decimal takeProfitPrice = isLong
                    ? position.EntryPrice * (1 + takeProfitMultiplier)
                    : position.EntryPrice * (1 - takeProfitMultiplier);

                bool takeProfitHit = isLong
                    ? currentPrice >= takeProfitPrice
                    : currentPrice <= takeProfitPrice;

                if (takeProfitHit)
                {
                    CloseBacktestPosition(position, currentPrice, ref currentEquity);
                    positions.Remove(position);
                }
            }
        }

        /// <summary>
        /// Закрытие позиции в бэктесте
        /// </summary>
        private void CloseBacktestPosition(BacktestPosition position, decimal closePrice, ref decimal currentEquity)
        {
            bool isLong = position.Direction == "Long";
            decimal profit = isLong
                ? (closePrice - position.EntryPrice) * position.Volume
                : (position.EntryPrice - closePrice) * position.Volume;

            position.Profit = profit;
            currentEquity += profit;
        }

        /// <summary>
        /// Расчет объема для бэктеста
        /// </summary>
        private decimal CalculateBacktestVolume(BotTabSimple tab, int currentPositionsCount, bool isLong)
        {
            try
            {
                decimal baseVolume = GetVolume(tab, isLong);
                if (baseVolume <= 0) return 0;

                if (VolumeType.ValueString != "Contracts")
                {
                    decimal reductionPercent = VolumeReductionPerOrder.ValueDecimal * currentPositionsCount;
                    decimal reductionFactor = Math.Max(0.1m, 1 - reductionPercent / 100m);
                    baseVolume *= reductionFactor;
                }

                return baseVolume;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region ЗАГРУЗКА ДАННЫХ ИЗ ФАЙЛОВ

        /// <summary>
        /// Загрузка исторических данных из txt файлов с улучшенной обработкой путей
        /// </summary>
        private List<Candle> LoadHistoricalDataFromFiles(string securityName)
        {
            try
            {
                List<Candle> allCandles = new List<Candle>();
                string basePath = BacktestFilesPath.ValueString;

                // Если путь пустой, используем текущую директорию + Data
                if (string.IsNullOrEmpty(basePath))
                {
                    basePath = "Data";
                }

                // Преобразуем относительный путь в абсолютный
                if (!System.IO.Path.IsPathRooted(basePath))
                {
                    string currentDir = System.IO.Directory.GetCurrentDirectory();
                    basePath = System.IO.Path.Combine(currentDir, basePath);
                }

                // Создаем директорию если она не существует
                if (!System.IO.Directory.Exists(basePath))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(basePath);
                        SendNewLogMessage($"📁 Создана директория для данных: {basePath}", LogMessageType.System);
                    }
                    catch (Exception dirEx)
                    {
                        SendNewLogMessage($"❌ Не удалось создать директорию {basePath}: {dirEx.Message}", LogMessageType.Error);
                        return allCandles;
                    }
                }

                // Ищем файлы с данными для данного инструмента (включая поддиректории)
                string[] searchPatterns = new[] { $"{securityName}*.txt", $"{securityName}*.csv", "*.txt", "*.csv" };
                List<string> allFiles = new List<string>();

                foreach (var pattern in searchPatterns)
                {
                    try
                    {
                        var files = System.IO.Directory.GetFiles(basePath, pattern, System.IO.SearchOption.AllDirectories);
                        allFiles.AddRange(files);
                    }
                    catch (Exception searchEx)
                    {
                        // Игнорируем ошибки поиска в отдельных поддиректориях
                        SendNewLogMessage($"⚠️ Ошибка поиска файлов по шаблону {pattern}: {searchEx.Message}", LogMessageType.System);
                    }
                }

                // Убираем дубликаты
                var distinctFiles = allFiles.Distinct().ToArray();

                if (!distinctFiles.Any())
                {
                    SendNewLogMessage($"⚠️ Не найдены файлы данных для {securityName} в директории {basePath}. Используем данные OsEngine.", LogMessageType.System);
                    return allCandles;
                }

                SendNewLogMessage($"🔍 Найдено {distinctFiles.Length} файлов данных для {securityName}", LogMessageType.System);

                foreach (var file in distinctFiles.OrderBy(f => f))
                {
                    try
                    {
                        var candles = ParseTextFile(file);
                        if (candles.Any())
                        {
                            allCandles.AddRange(candles);
                            SendNewLogMessage($"📄 Загружено {candles.Count} свечей из файла {System.IO.Path.GetFileName(file)}", LogMessageType.System);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        SendNewLogMessage($"⚠️ Ошибка загрузки файла {System.IO.Path.GetFileName(file)}: {fileEx.Message}", LogMessageType.System);
                    }
                }

                if (!allCandles.Any())
                {
                    SendNewLogMessage($"⚠️ Не удалось загрузить данные из файлов. Используем данные OsEngine.", LogMessageType.System);
                    return allCandles;
                }

                // Сортируем и удаляем дубликаты
                allCandles = allCandles
                    .OrderBy(c => c.TimeStart)
                    .GroupBy(c => c.TimeStart)
                    .Select(g => g.First())
                    .ToList();

                SendNewLogMessage($"✅ Загружено {allCandles.Count} уникальных свечей для {securityName}", LogMessageType.System);
                return allCandles;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Критическая ошибка загрузки данных из файлов для {securityName}: {ex.Message}", LogMessageType.Error);
                SendNewLogMessage("🔄 Переключаемся на использование данных OsEngine", LogMessageType.System);
                return new List<Candle>();
            }
        }

        /// <summary>
        /// Парсинг txt файла с историческими данными
        /// </summary>
        private List<Candle> ParseTextFile(string filePath)
        {
            var candles = new List<Candle>();

            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 7) continue; // Минимум: дата,время,O,H,L,C,V

                    try
                    {
                        // Парсим дату и время
                        string dateStr = parts[0].Trim();
                        string timeStr = parts[1].Trim();

                        if (dateStr.Length == 8 && timeStr.Length == 6) // Формат YYYYMMDD,HHMMSS
                        {
                            int year = int.Parse(dateStr.Substring(0, 4));
                            int month = int.Parse(dateStr.Substring(4, 2));
                            int day = int.Parse(dateStr.Substring(6, 2));

                            int hour = int.Parse(timeStr.Substring(0, 2));
                            int minute = int.Parse(timeStr.Substring(2, 2));
                            int second = int.Parse(timeStr.Substring(4, 2));

                            DateTime candleTime = new DateTime(year, month, day, hour, minute, second);

                            // Парсим OHLCV
                            decimal open = decimal.Parse(parts[2].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                            decimal high = decimal.Parse(parts[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                            decimal low = decimal.Parse(parts[4].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                            decimal close = decimal.Parse(parts[5].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                            decimal volume = decimal.Parse(parts[6].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);

                            var candle = new Candle()
                            {
                                TimeStart = candleTime,
                                Open = open,
                                High = high,
                                Low = low,
                                Close = close,
                                Volume = volume
                            };

                            candles.Add(candle);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Пропускаем некорректные строки
                        SendNewLogMessage($"⚠️ Пропущена некорректная строка в файле {System.IO.Path.GetFileName(filePath)}: {line}. Ошибка: {ex.Message}", LogMessageType.System);
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка чтения файла {System.IO.Path.GetFileName(filePath)}: {ex.Message}", LogMessageType.Error);
            }

            return candles;
        }

        /// <summary>
        /// Получение исторических данных для бэктеста
        /// </summary>
        private List<Candle> GetHistoricalDataForBacktest(BotTabSimple tab)
        {
            string securityName = tab.Security.Name;

            if (BacktestDataSource.ValueString == "TextFiles")
            {
                var fileData = LoadHistoricalDataFromFiles(securityName);
                if (fileData.Any())
                {
                    return fileData;
                }
                else
                {
                    SendNewLogMessage($"⚠️ Не удалось загрузить данные из файлов для {securityName}, используем данные OsEngine", LogMessageType.System);
                }
            }

            // По умолчанию используем данные OsEngine
            return tab.CandlesAll ?? new List<Candle>();
        }

        #endregion

        #region КЛАССЫ ИЗ ОРИГИНАЛЬНОГО РОБОТА

        private class PendingOrder
        {
            public string Security { get; set; }
            public BotTabSimple Tab { get; set; }
            public string Direction { get; set; } // "Long" или "Short"
            public decimal Volume { get; set; }
            public decimal Price { get; set; }
            public DateTime CreateTime { get; set; }
            public DateTime ExecuteTime { get; set; }
            public bool IsExecuted { get; set; }
        }

        private class GradedBlockResult
        {
            public bool AllowTrading { get; set; } = true;
            public decimal VolumeMultiplier { get; set; } = 1.0m;
            public string BlockReason { get; set; } = "";
        }

        public class PumpDetector
        {
            private Dictionary<string, DateTime> _lastPumpSignals = new Dictionary<string, DateTime>();
            private decimal _sensitivity;
            private decimal _minVolumeRatio;
            
            public decimal LastPriceChange { get; private set; }
            public decimal LastVolumeRatio { get; private set; }

            public PumpDetector(decimal sensitivity, decimal minVolumeRatio)
            {
                _sensitivity = sensitivity;
                _minVolumeRatio = minVolumeRatio;
            }

            public bool IsPump(string security, BotTabSimple tab, Candle currentCandle)
            {
                try
                {
                    var candles = tab.CandlesAll;
                    if (candles == null || candles.Count < 20) return false;

                    decimal priceChange = (currentCandle.Close - currentCandle.Open) / currentCandle.Open * 100;
                    LastPriceChange = priceChange;
                    bool priceCondition = priceChange > _sensitivity;
                    
                    decimal avgVolume = candles.Skip(candles.Count - 20).Take(20).Average(c => c.Volume);
                    decimal volumeRatio = currentCandle.Volume / avgVolume;
                    LastVolumeRatio = volumeRatio;
                    bool volumeCondition = volumeRatio > _minVolumeRatio;
                    
                    decimal historicalVolatility = CalculateHistoricalVolatility(candles, 20);
                    bool volatilityCondition = priceChange > historicalVolatility * 2.5m;
                    
                    bool isPump = priceCondition && volumeCondition && volatilityCondition;
                    
                    if (isPump && _lastPumpSignals.ContainsKey(security))
                    {
                        if (DateTime.Now - _lastPumpSignals[security] < TimeSpan.FromMinutes(10))
                        {
                            return false;
                        }
                    }
                    
                    if (isPump)
                    {
                        _lastPumpSignals[security] = DateTime.Now;
                    }
                    
                    return isPump;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private decimal CalculateHistoricalVolatility(List<Candle> candles, int period)
            {
                try
                {
                    decimal sumChanges = 0;
                    int count = 0;
                    
                    for (int i = Math.Max(0, candles.Count - period); i < candles.Count - 1; i++)
                    {
                        decimal change = Math.Abs((candles[i+1].Close - candles[i].Close) / candles[i].Close * 100);
                        sumChanges += change;
                        count++;
                    }
                    
                    return count > 0 ? sumChanges / count : 1.0m;
                }
                catch
                {
                    return 1.0m;
                }
            }
        }

        public class DumpDetector
        {
            private Dictionary<string, DateTime> _lastDumpSignals = new Dictionary<string, DateTime>();
            private decimal _sensitivity;
            private decimal _minVolumeRatio;
            
            public decimal LastPriceChange { get; private set; }
            public decimal LastVolumeRatio { get; private set; }

            public DumpDetector(decimal sensitivity, decimal minVolumeRatio)
            {
                _sensitivity = sensitivity;
                _minVolumeRatio = minVolumeRatio;
            }

            public bool IsDump(string security, BotTabSimple tab, Candle currentCandle)
            {
                try
                {
                    var candles = tab.CandlesAll;
                    if (candles == null || candles.Count < 20) return false;

                    decimal priceChange = (currentCandle.Close - currentCandle.Open) / currentCandle.Open * 100;
                    LastPriceChange = priceChange;
                    bool priceCondition = priceChange < _sensitivity;
                    
                    decimal avgVolume = candles.Skip(candles.Count - 20).Take(20).Average(c => c.Volume);
                    decimal volumeRatio = currentCandle.Volume / avgVolume;
                    LastVolumeRatio = volumeRatio;
                    bool volumeCondition = volumeRatio > _minVolumeRatio;
                    
                    decimal historicalVolatility = CalculateHistoricalVolatility(candles, 20);
                    bool volatilityCondition = Math.Abs(priceChange) > historicalVolatility * 2.5m;
                    
                    bool isDump = priceCondition && volumeCondition && volatilityCondition;
                    
                    if (isDump && _lastDumpSignals.ContainsKey(security))
                    {
                        if (DateTime.Now - _lastDumpSignals[security] < TimeSpan.FromMinutes(10))
                        {
                            return false;
                        }
                    }
                    
                    if (isDump)
                    {
                        _lastDumpSignals[security] = DateTime.Now;
                    }
                    
                    return isDump;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private decimal CalculateHistoricalVolatility(List<Candle> candles, int period)
            {
                try
                {
                    decimal sumChanges = 0;
                    int count = 0;
                    
                    for (int i = Math.Max(0, candles.Count - period); i < candles.Count - 1; i++)
                    {
                        decimal change = Math.Abs((candles[i+1].Close - candles[i].Close) / candles[i].Close * 100);
                        sumChanges += change;
                        count++;
                    }
                    
                    return count > 0 ? sumChanges / count : 1.0m;
                }
                catch
                {
                    return 1.0m;
                }
            }
        }

        public class TrendAnalysis
        {
            public string PrimaryTrend { get; private set; } = "Neutral";
            public string SecondaryTrend { get; private set; } = "Neutral";
            public decimal TrendStrength { get; private set; }
            public decimal Volatility { get; private set; }
            public bool IsStrongUptrend { get; private set; }
            public bool IsStrongDowntrend { get; private set; }
            public bool IsConsolidation { get; private set; }
            
            // Ишимоку компоненты (замена EMA)
            public decimal TenkanSen { get; private set; } // Conversion Line
            public decimal KijunSen { get; private set; }  // Base Line
            public decimal SenkouSpanA { get; private set; } // Leading Span A
            public decimal SenkouSpanB { get; private set; } // Leading Span B
            public decimal ChikouSpan { get; private set; } // Lagging Span
            
            public decimal Atr { get; private set; }
            public decimal Rsi { get; private set; }

            // Новые поля для дополнительных фильтров
            public decimal VolatilityPercent { get; private set; }
            public string TrendQuality { get; private set; } = "Low";
            public bool PriceAboveCloud { get; private set; }
            public bool PriceBelowCloud { get; private set; }
            public bool CloudBullish { get; private set; }
            public bool CloudBearish { get; private set; }

            public void Update(BotTabSimple tab, Candle currentCandle,
                             int tenkanPeriod, int kijunPeriod, int senkouBPeriod, int displacement,
                             int rsiPeriod, int atrPeriod)
            {
                try
                {
                    var candles = tab.CandlesAll;
                    if (candles == null || candles.Count < Math.Max(senkouBPeriod + displacement, 100)) return;

                    // Расчет компонентов Ишимоку
                    CalculateIchimokuComponents(candles, currentCandle, tenkanPeriod, kijunPeriod, senkouBPeriod, displacement);
                    
                    // Расчет RSI
                    Rsi = CalculateRSI(candles, rsiPeriod);
                    
                    // Расчет ATR и волатильности
                    Atr = CalculateATR(candles, atrPeriod);
                    VolatilityPercent = Atr / currentCandle.Close * 100;

                    // Определение положения цены относительно облака
                    PriceAboveCloud = currentCandle.Close > Math.Max(SenkouSpanA, SenkouSpanB);
                    PriceBelowCloud = currentCandle.Close < Math.Min(SenkouSpanA, SenkouSpanB);
                    
                    // Определение направления облака
                    CloudBullish = SenkouSpanA > SenkouSpanB;
                    CloudBearish = SenkouSpanA < SenkouSpanB;

                    // Определение основного тренда по Ишимоку
                    if (PriceAboveCloud && CloudBullish && TenkanSen > KijunSen)
                        PrimaryTrend = "Up";
                    else if (PriceBelowCloud && CloudBearish && TenkanSen < KijunSen)
                        PrimaryTrend = "Down";
                    else
                        PrimaryTrend = "Neutral";
                    
                    // Определение вторичного тренда
                    decimal priceMomentum = (currentCandle.Close - candles[Math.Max(0, candles.Count - 5)].Close) / 
                                           candles[Math.Max(0, candles.Count - 5)].Close * 100;
                    
                    if (TenkanSen > KijunSen && priceMomentum > 0.5m)
                        SecondaryTrend = "Up";
                    else if (TenkanSen < KijunSen && priceMomentum < -0.5m)
                        SecondaryTrend = "Down";
                    else
                        SecondaryTrend = "Neutral";

                    // Расчет силы тренда с учетом Ишимоку
                    decimal tenkanKijunDistance = Math.Abs(TenkanSen - KijunSen) / KijunSen * 100;
                    decimal cloudWidth = Math.Abs(SenkouSpanA - SenkouSpanB) / ((SenkouSpanA + SenkouSpanB) / 2) * 100;

                    TrendStrength = (Math.Abs(priceMomentum) + tenkanKijunDistance + cloudWidth) / 3;

                    // Определение качества тренда
                    TrendQuality = CalculateTrendQuality();

                    // Определение сильных трендов
                    IsStrongUptrend = PrimaryTrend == "Up" && SecondaryTrend == "Up" &&
                                     TrendStrength > 60 && Rsi < 70 && CloudBullish;
                    IsStrongDowntrend = PrimaryTrend == "Down" && SecondaryTrend == "Down" &&
                                       TrendStrength > 60 && Rsi > 30 && CloudBearish;
                    IsConsolidation = TrendStrength < 30 && VolatilityPercent < 2.0m;
                }
                catch
                {
                    // Игнорируем ошибки анализа
                }
            }

            private void CalculateIchimokuComponents(List<Candle> candles, Candle currentCandle,
                                                    int tenkanPeriod, int kijunPeriod, int senkouBPeriod, int displacement)
            {
                try
                {
                    // Tenkan-sen (Conversion Line)
                    TenkanSen = CalculateMidpoint(candles, Math.Min(candles.Count, tenkanPeriod));

                    // Kijun-sen (Base Line)
                    KijunSen = CalculateMidpoint(candles, Math.Min(candles.Count, kijunPeriod));

                    // Senkou Span A (Leading Span A)
                    SenkouSpanA = (TenkanSen + KijunSen) / 2;

                    // Senkou Span B (Leading Span B)
                    SenkouSpanB = CalculateMidpoint(candles, Math.Min(candles.Count, senkouBPeriod));

                    // Chikou Span (Lagging Span) - исправлено: берем цену displacement свечей назад
                    int chikouIndex = candles.Count - displacement - 1;
                    if (chikouIndex >= 0 && chikouIndex < candles.Count)
                        ChikouSpan = candles[chikouIndex].Close;
                    else
                        ChikouSpan = currentCandle.Close;
                }
                catch
                {
                    TenkanSen = currentCandle.Close;
                    KijunSen = currentCandle.Close;
                    SenkouSpanA = currentCandle.Close;
                    SenkouSpanB = currentCandle.Close;
                    ChikouSpan = currentCandle.Close;
                }
            }

            private decimal CalculateMidpoint(List<Candle> candles, int period)
            {
                if (candles.Count < period || period <= 0)
                    return candles.Last().Close;
                
                decimal highestHigh = decimal.MinValue;
                decimal lowestLow = decimal.MaxValue;
                
                int startIndex = Math.Max(0, candles.Count - period);
                for (int i = startIndex; i < candles.Count; i++)
                {
                    if (candles[i].High > highestHigh)
                        highestHigh = candles[i].High;
                    if (candles[i].Low < lowestLow)
                        lowestLow = candles[i].Low;
                }
                
                return (highestHigh + lowestLow) / 2;
            }

            private string CalculateTrendQuality()
            {
                if (TrendStrength > 70 && CloudBullish && PriceAboveCloud) return "Very High Bullish";
                if (TrendStrength > 70 && CloudBearish && PriceBelowCloud) return "Very High Bearish";
                if (TrendStrength > 50) return "High";
                if (TrendStrength > 30) return "Medium";
                return "Low";
            }



            private decimal CalculateRSI(List<Candle> candles, int period)
            {
                if (candles.Count < period + 1) return 50;

                decimal avgGain = 0;
                decimal avgLoss = 0;

                for (int i = candles.Count - period; i < candles.Count; i++)
                {
                    decimal change = candles[i].Close - candles[i-1].Close;
                    if (change > 0)
                        avgGain += change;
                    else
                        avgLoss += Math.Abs(change);
                }

                avgGain /= period;
                avgLoss /= period;

                for (int i = candles.Count - period; i < candles.Count; i++)
                {
                    decimal change = candles[i].Close - candles[i-1].Close;
                    decimal currentGain = change > 0 ? change : 0;
                    decimal currentLoss = change < 0 ? Math.Abs(change) : 0;
                    
                    avgGain = (avgGain * (period - 1) + currentGain) / period;
                    avgLoss = (avgLoss * (period - 1) + currentLoss) / period;
                }

                if (avgLoss == 0) return 100;
                
                decimal rs = avgGain / avgLoss;
                decimal rsi = 100 - (100 / (1 + rs));
                
                return Math.Max(0, Math.Min(100, rsi));
            }

            private decimal CalculateATR(List<Candle> candles, int period)
            {
                if (candles.Count < period + 1) return 0;

                decimal sumTrueRange = 0;
                int count = 0;

                for (int i = candles.Count - period; i < candles.Count; i++)
                {
                    decimal highLow = candles[i].High - candles[i].Low;
                    decimal highClose = Math.Abs(candles[i].High - candles[i-1].Close);
                    decimal lowClose = Math.Abs(candles[i].Low - candles[i-1].Close);
                    
                    decimal trueRange = Math.Max(highLow, Math.Max(highClose, lowClose));
                    sumTrueRange += trueRange;
                    count++;
                }

                return count > 0 ? sumTrueRange / count : 0;
            }
        }

        #endregion

        #region ОСНОВНЫЕ МЕТОДЫ

        public override string GetNameStrategyType() => "UniversalScreenerEngine";

        public override void ShowIndividualSettingsDialog()
        {
            // Базовая реализация для OsEngine
        }

        private void TabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            try
            {
                if (candles == null || candles.Count == 0 || tab?.Security == null)
                    return;

                string security = tab.Security.Name;

                // ДИАГНОСТИЧЕСКОЕ ЛОГИРОВАНИЕ
                SendNewLogMessage($"📊 Анализ {security}: свечей={candles.Count}, режим={TradingMode.ValueString}, Long={EnableLong.ValueBool}, Short={EnableShort.ValueBool}", LogMessageType.System);

                int minCandlesRequired = Math.Max(Math.Max(
                    IchimokuSenkouBPeriod.ValueInt + IchimokuDisplacement.ValueInt, 100),
                    AtrPeriod.ValueInt + 10);

                if (candles.Count < minCandlesRequired)
                {
                    if (!_lastLogTimes.ContainsKey(security + "_min_candles") ||
                        DateTime.Now - _lastLogTimes[security + "_min_candles"] > TimeSpan.FromMinutes(10))
                    {
                        SendNewLogMessage($"⏳ Ожидание данных: {security} - недостаточно свечей для Ишимоку ({candles.Count} из {minCandlesRequired})",
                                        LogMessageType.System);
                        _lastLogTimes[security + "_min_candles"] = DateTime.Now;
                    }
                    return;
                }

                SendNewLogMessage($"✅ Достаточно свечей для {security}: {candles.Count} >= {minCandlesRequired}", LogMessageType.System);

                if (EnableGeneticOptimization.ValueBool && UseRealTimeGeneticOptimization.ValueBool)
                {
                    CheckRealTimeOptimization();
                }

                ProcessPendingOrders();
                CleanInactiveInstruments();

                if (!CanTradeInstrument(security))
                {
                    SendNewLogMessage($"🚫 Инструмент {security} не может торговаться", LogMessageType.System);
                    return;
                }

                var currentCandle = candles[candles.Count - 1];

                // ОБНОВЛЯЕМ АНАЛИЗ ТРЕНДА
                UpdateTrendAnalysis(security, tab, currentCandle);

                if (UsePumpProtection.ValueBool)
                    IsPumpDetected(security, tab, currentCandle);

                if (UseDumpProtection.ValueBool)
                    IsDumpDetected(security, tab, currentCandle);

                if (TradingMode.ValueString == "On")
                {
                    if (EnableLong.ValueBool)
                    {
                        SendNewLogMessage($"🎯 Проверка LONG условий для {security}", LogMessageType.System);
                        CheckLongConditions(security, tab, currentCandle);
                    }

                    if (EnableShort.ValueBool)
                    {
                        SendNewLogMessage($"🎯 Проверка SHORT условий для {security}", LogMessageType.System);
                        CheckShortConditions(security, tab, currentCandle);
                    }
                }

                CheckExitConditions(security, tab, currentCandle);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Критическая ошибка анализа: {ex.Message}", LogMessageType.Error);
            }
        }

        #endregion

        #region СИСТЕМА ЗАДЕРЖЕК

        private bool CanOpenOrder(string security, string orderType, int customDelay = -1)
        {
            if (!UseTradeDelay.ValueBool)
                return true;

            string key = $"{security}_{orderType}";
            
            if (!_lastOrderTimes.ContainsKey(key))
            {
                _lastOrderTimes[key] = DateTime.MinValue;
                return true;
            }

            TimeSpan timeSinceLastOrder = DateTime.Now - _lastOrderTimes[key];
            int requiredDelay = customDelay >= 0 ? customDelay : GetRequiredDelay(security, orderType);

            if (timeSinceLastOrder.TotalSeconds < requiredDelay)
            {
                SendNewLogMessage(
                    $"⏳ Задержка {orderType} для {security}: " +
                    $"{timeSinceLastOrder.TotalSeconds:F1} сек из {requiredDelay} сек",
                    LogMessageType.System);
                return false;
            }

            return true;
        }

        private int GetRequiredDelay(string security, string orderType)
        {
            int baseDelay = orderType.Contains("First") ? 
                DelayBeforeOpenSeconds.ValueInt : 
                DelayBetweenOrdersSeconds.ValueInt;

            if (RandomDelayRange.ValueInt > 0)
            {
                Random rand = new Random(Guid.NewGuid().GetHashCode());
                int randomAddition = rand.Next(0, RandomDelayRange.ValueInt + 1);
                baseDelay += randomAddition;
            }

            return baseDelay;
        }

        private void UpdateLastOrderTime(string security, string orderType)
        {
            string key = $"{security}_{orderType}";
            _lastOrderTimes[key] = DateTime.Now;
        }

        private void CreatePendingOrder(string security, BotTabSimple tab, string direction, 
                                      decimal volume, decimal price, int delaySeconds)
        {
            string orderKey = $"{security}_{direction}_{DateTime.Now.Ticks}";
            
            var pendingOrder = new PendingOrder
            {
                Security = security,
                Tab = tab,
                Direction = direction,
                Volume = volume,
                Price = price,
                CreateTime = DateTime.Now,
                ExecuteTime = DateTime.Now.AddSeconds(delaySeconds),
                IsExecuted = false
            };

            _pendingOrders[orderKey] = pendingOrder;

            SendNewLogMessage(
                $"⏰ ОТЛОЖЕННЫЙ ОРДЕР: {security} {direction} | " +
                $"Объем: {volume:F8} | Цена: {price:F4} | " +
                $"Исполнение через: {delaySeconds} сек",
                LogMessageType.System);
        }

        private void ProcessPendingOrders()
        {
            var ordersToRemove = new List<string>();
            var ordersToExecute = new List<PendingOrder>();

            foreach (var order in _pendingOrders)
            {
                if (DateTime.Now >= order.Value.ExecuteTime && !order.Value.IsExecuted)
                {
                    ordersToExecute.Add(order.Value);
                    order.Value.IsExecuted = true;
                }

                if (order.Value.IsExecuted && DateTime.Now - order.Value.ExecuteTime > TimeSpan.FromMinutes(10))
                {
                    ordersToRemove.Add(order.Key);
                }
            }

            foreach (var order in ordersToExecute)
            {
                ExecutePendingOrder(order);
            }

            foreach (var orderKey in ordersToRemove)
            {
                _pendingOrders.Remove(orderKey);
            }
        }

        private void ExecutePendingOrder(PendingOrder order)
        {
            try
            {
                if (order.Volume <= 0)
                {
                    SendNewLogMessage($"❌ Отмена отложенного ордера {order.Security}: невалидный объем {order.Volume}", 
                                    LogMessageType.Error);
                    return;
                }

                if (order.Direction == "Long")
                {
                    order.Tab.BuyAtLimit(order.Volume, order.Price);
                }
                else
                {
                    order.Tab.SellAtLimit(order.Volume, order.Price);
                }

                string orderType = order.Tab.PositionsAll.Count == 0 ? "First" : "Subsequent";
                UpdateLastOrderTime(order.Security, orderType);

                SendNewLogMessage(
                    $"✅ ИСПОЛНЕН ОТЛОЖЕННЫЙ ОРДЕР: {order.Security} {order.Direction} | " +
                    $"Цена: {order.Price:F4} | Объем: {order.Volume:F8} | " +
                    $"Задержка: {(DateTime.Now - order.CreateTime).TotalSeconds:F1} сек",
                    LogMessageType.Trade);

                var newPosition = order.Tab.PositionsAll.LastOrDefault();
                if (newPosition != null)
                {
                    UpdatePositionTakeProfit(newPosition);
                    InitializeTrailingStop(newPosition);

                    // Регистрация позиции в риск-менеджере
                    if (_riskManager == null)
                    {
                        _riskManager = new RiskManagementComponent(this, _wentPositive);
                    }
                    _riskManager.RegisterPosition(newPosition.Number, newPosition.EntryPrice);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка исполнения отложенного ордера {order.Security}: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        private void OpenOrderWithDelay(string security, BotTabSimple tab, string direction, 
                                      decimal volume, decimal price, int customDelay = -1)
        {
            try
            {
                string orderType = tab.PositionsAll.Count == 0 ? "First" : "Subsequent";
                int requiredDelay = customDelay >= 0 ? customDelay : GetRequiredDelay(security, orderType);
                
                if (CanOpenOrder(security, orderType, requiredDelay))
                {
                    if (direction == "Long")
                    {
                        tab.BuyAtLimit(volume, price);
                    }
                    else
                    {
                        tab.SellAtLimit(volume, price);
                    }

                    UpdateLastOrderTime(security, orderType);

                    var newPosition = tab.PositionsAll.LastOrDefault();
                    if (newPosition != null)
                        UpdatePositionTakeProfit(newPosition);

                    SendNewLogMessage(
                        $"⚡ МГНОВЕННОЕ ИСПОЛНЕНИЕ: {security} {direction} | " +
                        $"Цена: {price:F4} | Объем: {volume:F8}",
                        LogMessageType.Trade);
                }
                else
                {
                    CreatePendingOrder(security, tab, direction, volume, price, requiredDelay);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка открытия ордера с задержкой {security}: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        #endregion

        #region РАСЧЕТ ОБЪЕМА (ПРАВИЛЬНЫЙ КАК В V32)

        /// <summary>
        /// Основной метод расчета объема - ТОЧНО КАК В V32
        /// </summary>
        private decimal GetVolume(BotTabSimple tab, bool isLong)
        {
            decimal volume = 0;

            try
            {
                if (tab?.Security == null)
                {
                    SendNewLogMessage("❌ Tab или Security is null в GetVolume", LogMessageType.Error);
                    return 0;
                }

                // Определяем цену в зависимости от направления
                decimal bestPrice = isLong ? tab.PriceBestAsk : tab.PriceBestBid;
                if (bestPrice <= 0)
                {
                    bestPrice = tab.CandlesAll.Last().Close;
                }

                // 1. ПО КОНТРАКТАМ ("Contracts")
                if (VolumeType.ValueString == "Contracts")
                {
                    volume = Volume.ValueDecimal;
                    
                    if (volume <= 0)
                    {
                        SendNewLogMessage($"❌ Объем Contracts должен быть > 0: {volume}", LogMessageType.Error);
                        return 0;
                    }
                    
                    // Для OsTrader: корректное округление с учетом спецификации инструмента
                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        volume = Math.Round(volume, tab.Security.DecimalsVolume);
                    }
                }
                // 2. ПО СТОИМОСТИ КОНТРАКТА ("Contract currency")
                else if (VolumeType.ValueString == "Contract currency")
                {
                    if (bestPrice <= 0)
                    {
                        SendNewLogMessage($"❌ Некорректная цена: {bestPrice}", LogMessageType.Error);
                        return 0;
                    }
                    
                    volume = Volume.ValueDecimal / bestPrice;
                    
                    // Для OsTrader применяем специальную логику с учетом лота (ТОЧНО КАК В V32)
                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        // Получаем разрешения сервера
                        IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);
                        
                        // Ключевое исправление: проверяем условия как в V32
                        // tab.Security.Lot != 0 && tab.Security.Lot > 1
                        if (serverPermission != null &&
                            serverPermission.IsUseLotToCalculateProfit &&
                            tab.Security.Lot != 0 &&
                            tab.Security.Lot > 1)  // ТОЧНО КАК В V32!
                        {
                            // Деление на лот только если лот > 1
                            volume = Volume.ValueDecimal / (bestPrice * tab.Security.Lot);
                        }
                        
                        // Всегда округляем по спецификации инструмента
                        volume = Math.Round(volume, tab.Security.DecimalsVolume);
                    }
                    else // Для тестера/оптимизатора
                    {
                        volume = Math.Round(volume, 6);
                    }
                }
                // 3. ПО ПРОЦЕНТУ ДЕПОЗИТА ("Deposit percent")
                else if (VolumeType.ValueString == "Deposit percent")
                {
                    Portfolio myPortfolio = tab.Portfolio;

                    if (myPortfolio == null)
                    {
                        SendNewLogMessage("❌ Portfolio is null", LogMessageType.Error);
                        return 0;
                    }

                    decimal portfolioPrimeAsset = 0;

                    if (TradeAssetInPortfolio.ValueString == "Prime")
                    {
                        portfolioPrimeAsset = myPortfolio.ValueCurrent;
                    }
                    else
                    {
                        List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                        if (positionOnBoard == null)
                        {
                            SendNewLogMessage("❌ PositionOnBoard is null", LogMessageType.Error);
                            return 0;
                        }

                        for (int i = 0; i < positionOnBoard.Count; i++)
                        {
                            if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                            {
                                portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                                break;
                            }
                        }
                    }

                    if (portfolioPrimeAsset == 0)
                    {
                        SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, LogMessageType.Error);
                        return 0;
                    }

                    decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);
                    
                    if (bestPrice <= 0)
                    {
                        SendNewLogMessage($"❌ Некорректная цена: {bestPrice}", LogMessageType.Error);
                        return 0;
                    }
                    
                    decimal qty = moneyOnPosition / bestPrice;

                    // Для OsTrader: деление на лот и округление по спецификации инструмента (ТОЧНО КАК В V32)
                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        // Получаем разрешения сервера
                        IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);
                        
                        // Ключевое исправление: проверяем условия как в V32
                        // tab.Security.Lot != 0 && tab.Security.Lot > 1
                        if (serverPermission != null &&
                            serverPermission.IsUseLotToCalculateProfit &&
                            tab.Security.Lot != 0 &&
                            tab.Security.Lot > 1)  // ТОЧНО КАК В V32!
                        {
                            // Делим на лот только если лот > 1
                            qty = moneyOnPosition / (bestPrice * tab.Security.Lot);
                        }
                        
                        qty = Math.Round(qty, tab.Security.DecimalsVolume);
                    }
                    else // Для тестера/оптимизатора
                    {
                        qty = Math.Round(qty, 7);
                    }
                    
                    return qty;
                }

                // ФИНАЛЬНАЯ ПРОВЕРКА ОБЪЕМА
                if (volume <= 0)
                {
                    SendNewLogMessage($"❌ Рассчитанный объем <= 0: {volume}", LogMessageType.Error);
                    return 0;
                }

                return volume;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Критическая ошибка в GetVolume: {ex.Message}", LogMessageType.Error);
                return 0;
            }
        }

        /// <summary>
        /// Расчет объема с учетом количества текущих позиций
        /// </summary>
        private decimal CalculateVolume(BotTabSimple tab, int currentPositionsCount, bool isLong)
        {
            try
            {
                decimal baseVolume = GetVolume(tab, isLong);
                
                if (baseVolume <= 0)
                {
                    SendNewLogMessage($"❌ Базовый объем <= 0: {baseVolume}", LogMessageType.Error);
                    return 0;
                }
                
                currentPositionsCount = Math.Max(0, currentPositionsCount);
                
                if (VolumeType.ValueString != "Contracts")
                {
                    decimal reductionPercent = VolumeReductionPerOrder.ValueDecimal * currentPositionsCount;
                    decimal reductionFactor = Math.Max(0.1m, 1 - reductionPercent / 100m);
                    
                    decimal finalVolume = baseVolume * reductionFactor;
                    
                    // Коррекция по лоту для OsTrader (после уменьшения объема)
                    if (StartProgram == StartProgram.IsOsTrader && finalVolume > 0)
                    {
                        finalVolume = Math.Round(finalVolume, tab.Security.DecimalsVolume);
                    }
                    
                    return finalVolume;
                }
                else
                {
                    return baseVolume;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка CalculateVolume: {ex.Message}", LogMessageType.Error);
                return 0;
            }
        }

        /// <summary>
        /// Расчет объема с учетом просадки
        /// </summary>
        private decimal CalculateVolumeWithDrawdownProtection(string security, BotTabSimple tab, int currentPositionsCount, bool isLong)
        {
            if (!UseDrawdownProtection.ValueBool)
                return CalculateVolume(tab, currentPositionsCount, isLong);

            try
            {
                decimal currentDrawdown = CalculateCurrentDrawdown(security, tab);
                
                if (!_instrumentDrawdowns.ContainsKey(security) || currentDrawdown > _instrumentDrawdowns[security])
                    _instrumentDrawdowns[security] = currentDrawdown;
                    
                decimal baseVolume = CalculateVolume(tab, currentPositionsCount, isLong);
                
                // Если просадка превышает лимит, уменьшаем объем
                if (_instrumentDrawdowns[security] > MaxDrawdownPerInstrument.ValueDecimal)
                {
                    decimal reduction = 1 - ((_instrumentDrawdowns[security] - MaxDrawdownPerInstrument.ValueDecimal) / MaxDrawdownPerInstrument.ValueDecimal * VolumeReductionFactor.ValueDecimal);
                    decimal protectedVolume = baseVolume * Math.Max(reduction, 0.1m);
                    
                    // Коррекция по лоту для OsTrader
                    if (StartProgram == StartProgram.IsOsTrader && protectedVolume > 0)
                    {
                        protectedVolume = Math.Round(protectedVolume, tab.Security.DecimalsVolume);
                    }
                    
                    return protectedVolume;
                }
                
                return baseVolume;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка защиты объема: {ex.Message}", LogMessageType.Error);
                return CalculateVolume(tab, currentPositionsCount, isLong);
            }
        }

        #endregion

        #region ОСНОВНАЯ ЛОГИКА ТОРГОВЛИ

        private void CheckLongConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            // ПРОВЕРКА ГЛОБАЛЬНОГО ЛИМИТА ПОЗИЦИЙ ПО ВСЕМ ИНСТРУМЕНТАМ
            int totalOpenPositions = GetTotalOpenPositions();
            if (totalOpenPositions >= MaxTradingInstruments.ValueInt)
            {
                SendNewLogMessage($"🚫 LONG БЛОКИРОВАН: достигнут глобальный лимит позиций ({totalOpenPositions} >= {MaxTradingInstruments.ValueInt})", LogMessageType.System);
                return;
            }

            var trendAnalysis = GetTrendAnalysis(security);

            // Получаем значения индикаторов
            decimal tenkanValue = trendAnalysis.TenkanSen;
            decimal kijunValue = trendAnalysis.KijunSen;
            decimal senkouAValue = trendAnalysis.SenkouSpanA;
            decimal senkouBValue = trendAnalysis.SenkouSpanB;
            decimal chikouValue = trendAnalysis.ChikouSpan;

            // Проверяем сигналы покупки по приоритету (как в Ichimoku GP)
            bool buySignal = CheckBuySignals(tenkanValue, kijunValue, currentCandle.Close,
                senkouAValue, senkouBValue, chikouValue);

            if (!buySignal)
            {
                return;
            }

            var positions = tab.PositionsAll;
            int openPositionsCount = positions.Count(p => p.State != PositionStateType.Done);

            if (openPositionsCount >= MaxOrdersCount.ValueInt)
            {
                SendNewLogMessage($"🚫 LONG БЛОКИРОВАН: достигнут лимит позиций ({openPositionsCount} >= {MaxOrdersCount.ValueInt})", LogMessageType.System);
                return;
            }

            decimal currentPrice = currentCandle.Close;

            // Проверка расстояния между ордерами
            bool distanceOk = ShouldOpenNextOrder(security, tab, currentPrice, "Long");

            if (distanceOk && !HasPositionNearPrice(tab, currentPrice))
            {
                int currentOpenPositions = positions.Count(p => p.State != PositionStateType.Done);
                decimal baseVolume = CalculateVolumeWithDrawdownProtection(security, tab, currentOpenPositions, true);

                if (baseVolume <= 0)
                {
                    SendNewLogMessage($"🚫 LONG БЛОКИРОВАН: объем <= 0", LogMessageType.System);
                    return;
                }

                // Масштабирование объема
                var volumeTrendAnalysis = GetTrendAnalysis(security);
                if (volumeTrendAnalysis.IsStrongUptrend)
                    baseVolume *= 1.2m;
                else if (volumeTrendAnalysis.PrimaryTrend == "Up")
                    baseVolume *= 1.1m;

                if (StartProgram == StartProgram.IsOsTrader && baseVolume > 0)
                {
                    baseVolume = Math.Round(baseVolume, tab.Security.DecimalsVolume);
                }

                // Экстренное уменьшение объема при дампе
                if (IsDumpDetected(security, tab, currentCandle))
                {
                    baseVolume = CalculateEmergencyVolume(baseVolume);
                    if (StartProgram == StartProgram.IsOsTrader && baseVolume > 0)
                    {
                        baseVolume = Math.Round(baseVolume, tab.Security.DecimalsVolume);
                    }
                }

                if (baseVolume > 0)
                {
                    decimal entryPrice = currentPrice + currentPrice * (Slippage.ValueDecimal / 100);
                    string signalReason = GetBuySignalReason(tenkanValue, kijunValue, currentPrice,
                        senkouAValue, senkouBValue, chikouValue);
                    SendNewLogMessage($"🎯 LONG СИГНАЛ: {signalReason}, цена {entryPrice:F4}, объем {baseVolume:F8}", LogMessageType.System);
                    OpenOrderWithDelay(security, tab, "Long", baseVolume, entryPrice);
                }
            }
        }

        private void CheckShortConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            // ПРОВЕРКА ГЛОБАЛЬНОГО ЛИМИТА ПОЗИЦИЙ ПО ВСЕМ ИНСТРУМЕНТАМ
            int totalOpenPositions = GetTotalOpenPositions();
            if (totalOpenPositions >= MaxTradingInstruments.ValueInt)
            {
                SendNewLogMessage($"🚫 SHORT БЛОКИРОВАН: достигнут глобальный лимит позиций ({totalOpenPositions} >= {MaxTradingInstruments.ValueInt})", LogMessageType.System);
                return;
            }

            var trendAnalysis = GetTrendAnalysis(security);

            // Получаем значения индикаторов
            decimal tenkanValue = trendAnalysis.TenkanSen;
            decimal kijunValue = trendAnalysis.KijunSen;

            // Проверяем сигналы продажи по приоритету (как в Ichimoku GP)
            bool sellSignal = CheckSellSignals(tenkanValue, kijunValue);

            if (!sellSignal)
            {
                return;
            }

            var positions = tab.PositionsAll;
            int openPositionsCount = positions.Count(p => p.State != PositionStateType.Done);

            if (openPositionsCount >= MaxOrdersCount.ValueInt)
            {
                SendNewLogMessage($"🚫 SHORT БЛОКИРОВАН: достигнут лимит позиций ({openPositionsCount} >= {MaxOrdersCount.ValueInt})", LogMessageType.System);
                return;
            }

            decimal currentPrice = currentCandle.Close;

            // Проверка расстояния между ордерами
            bool distanceOk = ShouldOpenNextOrder(security, tab, currentPrice, "Short");

            if (distanceOk && !HasPositionNearPrice(tab, currentPrice))
            {
                int currentOpenPositions = positions.Count(p => p.State != PositionStateType.Done);
                decimal baseVolume = CalculateVolumeWithDrawdownProtection(security, tab, currentOpenPositions, false);

                if (baseVolume <= 0)
                {
                    SendNewLogMessage($"🚫 SHORT БЛОКИРОВАН: объем <= 0", LogMessageType.System);
                    return;
                }

                // Масштабирование объема
                var volumeTrendAnalysis = GetTrendAnalysis(security);
                if (volumeTrendAnalysis.IsStrongDowntrend)
                    baseVolume *= 1.2m;
                else if (volumeTrendAnalysis.PrimaryTrend == "Down")
                    baseVolume *= 1.1m;

                if (StartProgram == StartProgram.IsOsTrader && baseVolume > 0)
                {
                    baseVolume = Math.Round(baseVolume, tab.Security.DecimalsVolume);
                }

                // Экстренное уменьшение объема при пампе
                if (IsPumpDetected(security, tab, currentCandle))
                {
                    baseVolume = CalculateEmergencyVolume(baseVolume);
                    if (StartProgram == StartProgram.IsOsTrader && baseVolume > 0)
                    {
                        baseVolume = Math.Round(baseVolume, tab.Security.DecimalsVolume);
                    }
                }

                if (baseVolume > 0)
                {
                    decimal entryPrice = currentPrice - currentPrice * (Slippage.ValueDecimal / 100);
                    string signalReason = GetSellSignalReason(tenkanValue, kijunValue);
                    SendNewLogMessage($"🎯 SHORT СИГНАЛ: {signalReason}, цена {entryPrice:F4}, объем {baseVolume:F8}", LogMessageType.System);
                    OpenOrderWithDelay(security, tab, "Short", baseVolume, entryPrice);
                }
            }
        }

        /// <summary>
        /// Проверка сигналов покупки по приоритету (как в Ichimoku GP)
        /// </summary>
        private bool CheckBuySignals(decimal tenkan, decimal kijun, decimal currentPrice,
            decimal senkouA, decimal senkouB, decimal chikou)
        {
            bool signal = false;
            string signalType = "";

            // Counterintuitive логика входа (вход на откате при тренде) - ПРИОРИТЕТ 1 - commented out due to missing components
            // if (UseCounterintuitive.ValueString == "Включено" && CounterintuitiveEntry.ValueString == "Включено")
            // {
            //     var dataComponent = GetComponent<DataIndicatorComponent>();
            //     if (dataComponent != null && dataComponent.TryGetCounterintuitiveEmaValues(out decimal ema1, out decimal ema2, out decimal ema3))
            //     {
            //         // Логика counterintuitive: тренд вверх (ema2 > ema1) и цена в откате ниже быстрой и контртрендовой EMA
            //         if (ema2 > ema1 && currentPrice < ema2 && currentPrice < ema3)
            //         {
            //             signal = true;
            //             signalType = $"Counterintuitive: EMA2({ema2:F4}) > EMA1({ema1:F4}) [тренд], цена({currentPrice:F4}) < EMA2 и < EMA3 [откат]";
            //         }
            //     }
            // }

            // Пересечение Тенкан/Киджун - ПРИОРИТЕТ 2
            if (!signal && OpenByTkKj.ValueString == "Включено" && tenkan > kijun)
            {
                signal = true;
                signalType = "Ишимоку: Пересечение Тенкан/Киджун вверх";
            }

            // Цена выше облака - ПРИОРИТЕТ 3
            if (!signal && OpenByCloud.ValueString == "Включено" && IsPriceAboveCloud(currentPrice, senkouA, senkouB))
            {
                signal = true;
                signalType = "Ишимоку: Цена выше облака";
            }

            // Чикоу Спан выше цены - ПРИОРИТЕТ 4
            if (!signal && OpenByChikou.ValueString == "Включено" && IsChikouAbovePrice(chikou, currentPrice))
            {
                signal = true;
                signalType = "Ишимоку: Чикоу Спан выше цены";
            }

            // Сохраняем тип сигнала для использования в TryOpenLongPosition
            if (signal && !string.IsNullOrEmpty(signalType))
            {
                // В Neirobot нет SharedData для сигналов, просто возвращаем результат
            }

            return signal;
        }

        /// <summary>
        /// Проверка сигналов продажи (упрощенная версия для SHORT)
        /// </summary>
        private bool CheckSellSignals(decimal tenkan, decimal kijun)
        {
            // Counterintuitive логика входа для SHORT - commented out due to missing components
            // if (UseCounterintuitive.ValueString == "Включено" && CounterintuitiveEntry.ValueString == "Включено")
            // {
            //     var dataComponent = GetComponent<DataIndicatorComponent>();
            //     if (dataComponent != null && dataComponent.TryGetCounterintuitiveEmaValues(out decimal ema1, out decimal ema2, out decimal ema3))
            //     {
            //         var tab = GetTabBySecurityName("");
            //         if (tab != null && tab.CandlesAll != null && tab.CandlesAll.Count > 0)
            //         {
            //             decimal currentPrice = tab.CandlesAll.Last().Close;
            //
            //             // Логика counterintuitive: тренд вниз (ema2 < ema1) и цена в откате выше быстрой и контртрендовой EMA
            //             if (ema2 < ema1 && currentPrice > ema2 && currentPrice > ema3)
            //             {
            //                 return true;
            //             }
            //         }
            //     }
            // }

            // Пересечение Тенкан/Киджун вниз
            if (OpenByTkKj.ValueString == "Включено" && tenkan < kijun)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Получение причины сигнала покупки
        /// </summary>
        private string GetBuySignalReason(decimal tenkan, decimal kijun, decimal currentPrice,
            decimal senkouA, decimal senkouB, decimal chikou)
        {
            if (OpenByTkKj.ValueString == "Включено" && tenkan > kijun)
                return "Пересечение Тенкан/Киджун";

            if (OpenByCloud.ValueString == "Включено" && IsPriceAboveCloud(currentPrice, senkouA, senkouB))
                return "Цена выше облака";

            if (OpenByChikou.ValueString == "Включено" && IsChikouAbovePrice(chikou, currentPrice))
                return "Чикоу Спан выше цены";

            return "Сигнал Ишимоку";
        }

        /// <summary>
        /// Получение причины сигнала продажи
        /// </summary>
        private string GetSellSignalReason(decimal tenkan, decimal kijun)
        {
            if (OpenByTkKj.ValueString == "Включено" && tenkan < kijun)
                return "Пересечение Тенкан/Киджун вниз";

            return "Сигнал на SHORT (условия индикаторов)";
        }

        /// <summary>
        /// Проверка: цена выше облака (верхняя граница = max(Senkou A, Senkou span B)).
        /// </summary>
        private bool IsPriceAboveCloud(decimal price, decimal senkouA, decimal senkouB)
        {
            return price > Math.Max(senkouA, senkouB);
        }

        /// <summary>
        /// Проверка: Чикоу Спан выше текущей цены.
        /// </summary>
        private bool IsChikouAbovePrice(decimal chikou, decimal price)
        {
            return chikou > price;
        }

        private void CheckExitConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            try
            {
                var positions = tab.PositionsAll;
                decimal currentPrice = currentCandle.Close;

                foreach (var position in positions)
                {
                    if (position.State == PositionStateType.Done)
                        continue;

                    bool isLong = position.Direction == Side.Buy;

                    // Обновляем статус позиции в риск-менеджере (была ли в плюсе)
                    if (_riskManager != null)
                    {
                        _riskManager.UpdatePositionStatus(position.Number, currentPrice, isLong);
                    }

                    // Обновляем трейлинг-стоп для позиции
                    if (UseTrailingStop.ValueBool)
                    {
                        CheckAndUpdateTrailing(position.Number, currentPrice, isLong);
                    }

                    decimal takeProfitLevel = GetPositionTakeProfit(position);
                    decimal trailingLevel = UseTrailingStop.ValueBool ? GetPositionTrailingLevel(position.Number) : 0;

                    bool takeProfitHit = isLong ?
                        currentPrice >= takeProfitLevel :
                        currentPrice <= takeProfitLevel;

                    bool trailingStopHit = false;
                    if (UseTrailingStop.ValueBool && trailingLevel != 0)
                    {
                        trailingStopHit = isLong ?
                            currentPrice <= trailingLevel :
                            currentPrice >= trailingLevel;
                    }

                    bool forceClose = TradingMode.ValueString == "Only Close Position";

                    if (takeProfitHit || trailingStopHit || forceClose)
                    {
                        string exitReason = forceClose ? "принудительное закрытие" :
                                          trailingStopHit ? "трейлинг-стоп" : "тейк-профит";

                        // Используем безубыточную логику закрытия вместо прямого закрытия
                        TryClosePosition(position, currentPrice, exitReason);

                        // Очищаем данные после успешного закрытия (в TryClosePosition уже логируется)
                        string positionKey = $"{position.SecurityName}_{position.Number}";
                        if (_positionTakeProfits.ContainsKey(positionKey))
                            _positionTakeProfits.Remove(positionKey);

                        // Обновляем параметры самообучаемого трейлинга при закрытии позиции
                        if (UseTrailingStop.ValueBool && TrailingType.ValueString == "SelfLearning")
                        {
                            UpdateSelfLearningParameters(position, currentPrice, trailingStopHit, exitReason);
                        }

                        // Очищаем данные трейлинг-стопа
                        if (UseTrailingStop.ValueBool)
                        {
                            ClearTrailingStopData(position.Number);
                        }

                        // Удаляем позицию из риск-менеджера
                        if (_riskManager != null)
                        {
                            _riskManager.RemovePosition(position.Number);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка проверки выхода {security}: {ex.Message}", LogMessageType.Error);
            }
        }

        #endregion

        #region ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ

        private void UpdateTrendAnalysis(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!_instrumentTrends.ContainsKey(security))
            {
                _instrumentTrends[security] = new TrendAnalysis();
            }

            var trendAnalysis = _instrumentTrends[security];
            trendAnalysis.Update(tab, currentCandle,
                               IchimokuTenkanPeriod.ValueInt,
                               IchimokuKijunPeriod.ValueInt,
                               IchimokuSenkouBPeriod.ValueInt,
                               IchimokuDisplacement.ValueInt,
                               RsiPeriod.ValueInt,
                               AtrPeriod.ValueInt);
        }

        private TrendAnalysis GetTrendAnalysis(string security)
        {
            return _instrumentTrends.ContainsKey(security) ? _instrumentTrends[security] : new TrendAnalysis();
        }

        private bool IsStrongUptrend(BotTabSimple tab)
        {
            try
            {
                var trendAnalysis = GetTrendAnalysis(tab.Security.Name);
                return trendAnalysis.IsStrongUptrend;
            }
            catch
            {
                return false;
            }
        }

        private bool IsStrongDowntrend(BotTabSimple tab)
        {
            try
            {
                var trendAnalysis = GetTrendAnalysis(tab.Security.Name);
                return trendAnalysis.IsStrongDowntrend;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateExtremePrices(string security, Candle currentCandle)
        {
            decimal currentHigh = currentCandle.High;
            decimal currentLow = currentCandle.Low;
            
            string currentTrend = GetCurrentTrend(security, currentCandle);
            
            if (!_lastTrendDirection.ContainsKey(security))
            {
                _lastTrendDirection[security] = currentTrend;
            }
            else if (_lastTrendDirection[security] != currentTrend)
            {
                _maxPrices[security] = currentHigh;
                _minPrices[security] = currentLow;
                _lastTrendDirection[security] = currentTrend;
            }
            
            if (!_maxPrices.ContainsKey(security) || currentHigh > _maxPrices[security])
                _maxPrices[security] = currentHigh;

            if (!_minPrices.ContainsKey(security) || currentLow < _minPrices[security])
                _minPrices[security] = currentLow;
        }

        private string GetCurrentTrend(string security, Candle currentCandle)
        {
            try
            {
                var tab = GetTabBySecurityName(security);
                if (tab == null) return "Neutral";
                
                var trendAnalysis = GetTrendAnalysis(security);
                return trendAnalysis.PrimaryTrend;
            }
            catch
            {
                return "Neutral";
            }
        }

        private BotTabSimple GetTabBySecurityName(string securityName)
        {
            return TabScreener.Tabs.ToList().Find(tab => 
                tab.Security.Name == securityName) as BotTabSimple;
        }

        private decimal CalculateCurrentDrawdown(string security, BotTabSimple tab)
        {
            try
            {
                var positions = tab.PositionsAll.Where(p => p.State != PositionStateType.Done).ToList();
                if (!positions.Any()) return 0;
                
                decimal totalUnrealizedProfit = positions.Sum(p => GetPositionProfit(p));
                decimal portfolioValue = GetPortfolioValue(tab);
                
                if (portfolioValue <= 0) return 0;
                
                decimal drawdownPercent = Math.Max(0, -totalUnrealizedProfit / portfolioValue * 100);
                
                return drawdownPercent;
            }
            catch
            {
                return 0;
            }
        }

        private decimal GetPositionProfit(Position position)
        {
            try
            {
                var type = position.GetType();
                
                var profitProperty = type.GetProperty("Profit");
                if (profitProperty != null)
                {
                    return (decimal)profitProperty.GetValue(position);
                }
                
                var profitPortfolioPunctProperty = type.GetProperty("ProfitPortfolioPunct");
                if (profitPortfolioPunctProperty != null)
                {
                    return (decimal)profitPortfolioPunctProperty.GetValue(position);
                }
                
                var profitOperationPunktProperty = type.GetProperty("ProfitOperationPunkt");
                if (profitOperationPunktProperty != null)
                {
                    return (decimal)profitOperationPunktProperty.GetValue(position);
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private decimal GetPortfolioValue(BotTabSimple tab)
        {
            try
            {
                return tab.Portfolio?.ValueCurrent ?? 10000m;
            }
            catch
            {
                return 10000m;
            }
        }

        private decimal GetLastOrderPriceInChain(string security, BotTabSimple tab, string direction)
        {
            try
            {
                var positions = tab.PositionsAll
                    .Where(p => p.State != PositionStateType.Done)
                    .Where(p => p.Direction == (direction == "Long" ? Side.Buy : Side.Sell))
                    .ToList();

                if (!positions.Any())
                    return 0;

                Position lastOrder = positions.OrderByDescending(p => p.TimeCreate).First();
                return lastOrder.EntryPrice;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка получения цены последнего ордера {security}: {ex.Message}", LogMessageType.Error);
                return 0;
            }
        }

        private bool ShouldOpenNextOrder(string security, BotTabSimple tab, decimal currentPrice, string direction)
        {
            try
            {
                if (TradingMode.ValueString == "Off") return false;
                if (TradingMode.ValueString == "Only Close Position") return false;

                decimal lastOrderPrice = GetLastOrderPriceInChain(security, tab, direction);

                if (lastOrderPrice == 0) return true;

                decimal requiredDistancePercent = direction == "Long" 
                    ? DistanceBetweenLongOrders.ValueDecimal 
                    : DistanceBetweenShortOrders.ValueDecimal;

                if (tab.PositionsAll.Count(p => p.State != PositionStateType.Done) <= 1)
                {
                    requiredDistancePercent *= 1.5m;
                }

                if (UseDynamicDistance.ValueBool)
                {
                    var trendAnalysis = GetTrendAnalysis(security);
                    decimal atrMultiplier = BaseDistanceAtrMultiplier.ValueDecimal;
                    decimal dynamicDistance = trendAnalysis.Atr / currentPrice * 100 * atrMultiplier;
                    
                    requiredDistancePercent = Math.Max(requiredDistancePercent, dynamicDistance);
                }
                
                decimal requiredDistance = requiredDistancePercent / 100m;
                
                bool shouldOpen = direction == "Long" 
                    ? currentPrice <= lastOrderPrice * (1 - requiredDistance)
                    : currentPrice >= lastOrderPrice * (1 + requiredDistance);
                
                return shouldOpen;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка проверки расстояния {security}: {ex.Message}", LogMessageType.Error);
                return false;
            }
        }

        private bool HasPositionNearPrice(BotTabSimple tab, decimal currentPrice)
        {
            var positions = tab.PositionsAll;
            
            foreach (var position in positions)
            {
                if (position.State == PositionStateType.Done) continue;
                    
                decimal priceDiff = Math.Abs(position.EntryPrice - currentPrice);
                decimal diffPercent = priceDiff / position.EntryPrice * 100;
                
                if (diffPercent < 0.1m) // фиксированное минимальное расстояние
                    return true;
            }
            
            return false;
        }

        private bool CanTradeInstrument(string security)
        {
            if (_activeInstruments.Count < MaxTradingInstruments.ValueInt)
            {
                if (!_activeInstruments.ContainsKey(security))
                {
                    _activeInstruments[security] = DateTime.Now;
                }
                return true;
            }

            if (_activeInstruments.ContainsKey(security))
            {
                _activeInstruments[security] = DateTime.Now;
                return true;
            }

            var oldestInstrument = _activeInstruments.OrderBy(x => x.Value).First();
            var oldestTab = GetTabBySecurityName(oldestInstrument.Key);
            if (oldestTab != null && oldestTab.PositionsAll.Count(p => p.State != PositionStateType.Done) == 0)
            {
                _activeInstruments.Remove(oldestInstrument.Key);
                _activeInstruments[security] = DateTime.Now;
                return true;
            }

            return false;
        }

        private void CleanInactiveInstruments()
        {
            var inactiveThreshold = TimeSpan.FromHours(1);
            var now = DateTime.Now;
            var toRemove = new List<string>();

            foreach (var instrument in _activeInstruments)
            {
                var tab = GetTabBySecurityName(instrument.Key);
                if (tab != null && tab.PositionsAll.Count(p => p.State != PositionStateType.Done) == 0 &&
                    now - instrument.Value > inactiveThreshold)
                {
                    toRemove.Add(instrument.Key);
                }
            }

            foreach (var instrumentKey in toRemove)
            {
                _activeInstruments.Remove(instrumentKey);
            }
        }

        /// <summary>
        /// Получение общего количества открытых позиций по всем инструментам
        /// </summary>
        private int GetTotalOpenPositions()
        {
            try
            {
                int totalOpenPositions = 0;

                foreach (var tab in TabScreener.Tabs)
                {
                    if (tab is BotTabSimple botTab)
                    {
                        int openPositionsCount = botTab.PositionsAll.Count(p => p.State != PositionStateType.Done);
                        totalOpenPositions += openPositionsCount;
                    }
                }

                return totalOpenPositions;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка подсчета общего количества позиций: {ex.Message}", LogMessageType.Error);
                return 0;
            }
        }

        #endregion

        #region ГРАДУИРОВАННАЯ БЛОКИРОВКА ШОРТОВ

        private GradedBlockResult ApplyGradedShortBlocking(string security, TrendAnalysis trendAnalysis)
        {
            var result = new GradedBlockResult();

            if (!UseGradedShortBlocking.ValueBool || trendAnalysis.PrimaryTrend != "Up")
                return result;

            decimal trendStrength = trendAnalysis.TrendStrength;

            if (trendStrength >= ShortBlockStrengthThreshold3.ValueDecimal)
            {
                result.AllowTrading = false;
                result.VolumeMultiplier = 0m;
            }
            else if (trendStrength >= ShortBlockStrengthThreshold2.ValueDecimal)
            {
                result.AllowTrading = true;
                result.VolumeMultiplier = (100 - ShortVolumeReduction2.ValueDecimal) / 100m;
            }
            else if (trendStrength >= ShortBlockStrengthThreshold1.ValueDecimal)
            {
                result.AllowTrading = true;
                result.VolumeMultiplier = (100 - ShortVolumeReduction1.ValueDecimal) / 100m;
            }
            else
            {
                result.AllowTrading = true;
                result.VolumeMultiplier = 1.0m;
            }

            return result;
        }

        private int GetShortSpecificDelay(string security, TrendAnalysis trendAnalysis, bool isFirstOrder)
        {
            if (!UseDifferentShortDelays.ValueBool || !UseTradeDelay.ValueBool)
                return GetRequiredDelay(security, isFirstOrder ? "First" : "Subsequent");

            int baseDelay = isFirstOrder ? DelayBeforeOpenSeconds.ValueInt : DelayBetweenOrdersSeconds.ValueInt;
            decimal multiplier = 1.0m;

            if (trendAnalysis.IsStrongUptrend)
            {
                multiplier = ShortDelayMultiplierStrongUptrend.ValueDecimal;
            }
            else if (trendAnalysis.PrimaryTrend == "Up" && trendAnalysis.TrendStrength > 40)
            {
                multiplier = ShortDelayMultiplierUptrend.ValueDecimal;
            }

            int finalDelay = (int)(baseDelay * multiplier);

            if (RandomDelayRange.ValueInt > 0)
            {
                Random rand = new Random(Guid.NewGuid().GetHashCode());
                int randomAddition = rand.Next(0, RandomDelayRange.ValueInt + 1);
                finalDelay += randomAddition;
            }

            return finalDelay;
        }

        #endregion

        #region ЗАЩИТА ОТ ПАМПА И ДАМПА

        private PumpDetector GetPumpDetector(string security)
        {
            if (!_pumpDetectors.ContainsKey(security))
            {
                _pumpDetectors[security] = new PumpDetector(
                    PumpDetectionSensitivity.ValueDecimal,
                    MinVolumeSpikeRatio.ValueDecimal);
            }
            return _pumpDetectors[security];
        }

        private bool IsPumpDetected(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!UsePumpProtection.ValueBool) 
                return false;

            try
            {
                var pumpDetector = GetPumpDetector(security);
                return pumpDetector.IsPump(security, tab, currentCandle);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка детектирования пампа {security}: {ex.Message}", LogMessageType.Error);
                return false;
            }
        }

        private bool ShouldBlockShorts(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!BlockShortsOnPump.ValueBool) 
                return false;

            bool isPump = IsPumpDetected(security, tab, currentCandle);
            bool isStrongUptrend = IsStrongUptrend(tab);
            
            return isPump || isStrongUptrend;
        }

        private decimal CalculateEmergencyVolume(decimal baseVolume)
        {
            return baseVolume * EmergencyVolumeReduction.ValueDecimal;
        }

        private DumpDetector GetDumpDetector(string security)
        {
            if (!_dumpDetectors.ContainsKey(security))
            {
                _dumpDetectors[security] = new DumpDetector(
                    DumpDetectionSensitivity.ValueDecimal,
                    MinVolumeSpikeRatio.ValueDecimal);
            }
            return _dumpDetectors[security];
        }

        private bool IsDumpDetected(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!UseDumpProtection.ValueBool) 
                return false;

            try
            {
                var dumpDetector = GetDumpDetector(security);
                return dumpDetector.IsDump(security, tab, currentCandle);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка детектирования дампа {security}: {ex.Message}", LogMessageType.Error);
                return false;
            }
        }

        private bool ShouldBlockLongs(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!BlockLongsOnDump.ValueBool) 
                return false;

            bool isDump = IsDumpDetected(security, tab, currentCandle);
            bool isStrongDowntrend = IsStrongDowntrend(tab);
            
            return isDump || isStrongDowntrend;
        }

        #endregion



        #region УПРАВЛЕНИЕ ТЕЙК-ПРОФИТОМ И ТРЕЙЛИНГ-СТОПОМ

        private void UpdatePositionTakeProfit(Position position)
        {
            string positionKey = $"{position.SecurityName}_{position.Number}";
            decimal takeProfit = CalculateTakeProfit(position);
            _positionTakeProfits[positionKey] = takeProfit;
        }

        private decimal GetPositionTakeProfit(Position position)
        {
            string positionKey = $"{position.SecurityName}_{position.Number}";
            if (_positionTakeProfits.ContainsKey(positionKey))
                return _positionTakeProfits[positionKey];

            return CalculateTakeProfit(position);
        }

        private decimal CalculateTakeProfit(Position position)
        {
            bool isLong = position.Direction == Side.Buy;
            decimal baseTakeProfitPercent = isLong ? TakeProfitLong.ValueDecimal : TakeProfitShort.ValueDecimal;

            decimal multiplier = baseTakeProfitPercent / 100m;

            decimal takeProfitPrice = isLong
                ? position.EntryPrice * (1 + multiplier)
                : position.EntryPrice * (1 - multiplier);

            return takeProfitPrice;
        }

        #endregion

        #region ТРЕЙЛИНГ-СТОП

        /// <summary>
        /// Расчет уровня трейлинг-стопа от экстремума (классический подход)
        /// </summary>
        private decimal CalculateTrailingLevel(int positionId, decimal currentPrice, bool isLong)
        {
            try
            {
                decimal distance = 0;

                if (TrailingType.ValueString == "SelfLearning")
                {
                    // Самообучаемый трейлинг
                    distance = CalculateSelfLearningDistance(positionId, currentPrice, isLong);
                }
                else if (TrailingType.ValueString == "ATR")
                {
                    // ATR трейлинг - от максимума для LONG, от минимума для SHORT
                    var trendAnalysis = GetTrendAnalysis(""); // Получаем ATR из анализа
                    decimal atrDistance = trendAnalysis.Atr * TrailingAtrMultiplier.ValueDecimal;
                    distance = atrDistance;
                }
                else if (TrailingType.ValueString == "Adaptive")
                {
                    // Адаптивный трейлинг - процент от текущей цены
                    distance = currentPrice * (AdaptiveDistance.ValueDecimal / 100m);
                }
                else // Fixed
                {
                    // Фиксированный процент
                    distance = currentPrice * (TrailingDistancePercent.ValueDecimal / 100m);
                }

                if (isLong)
                {
                    // Для LONG: уровень = максимум - расстояние
                    if (_highestPricesSinceEntry.ContainsKey(positionId))
                    {
                        decimal highestPrice = _highestPricesSinceEntry[positionId];
                        return highestPrice - distance;
                    }
                    else
                    {
                        // Если максимум не отслеживается, используем текущую цену
                        return currentPrice - distance;
                    }
                }
                else
                {
                    // Для SHORT: уровень = минимум + расстояние
                    if (_lowestPricesSinceEntry.ContainsKey(positionId))
                    {
                        decimal lowestPrice = _lowestPricesSinceEntry[positionId];
                        return lowestPrice + distance;
                    }
                    else
                    {
                        // Если минимум не отслеживается, используем текущую цену
                        return currentPrice + distance;
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка расчета трейлинг-уровня: {ex.Message}", LogMessageType.Error);
                return isLong ? currentPrice - currentPrice * (TrailingDistancePercent.ValueDecimal / 100m)
                             : currentPrice + currentPrice * (TrailingDistancePercent.ValueDecimal / 100m);
            }
        }

        /// <summary>
        /// Расчет расстояния для самообучаемого трейлинга
        /// </summary>
        private decimal CalculateSelfLearningDistance(int positionId, decimal currentPrice, bool isLong)
        {
            try
            {
                // Получаем данные позиции
                if (!_positionTrailingHistory.ContainsKey(positionId))
                    return SelfLearningBaseDistance.ValueDecimal; // Базовое расстояние если нет истории

                var history = _positionTrailingHistory[positionId];
                var security = history.Security;

                // Получаем данные обучения для инструмента
                if (!_trailingLearningData.ContainsKey(security))
                {
                    _trailingLearningData[security] = new TrailingLearningData(security);
                }

                var learningData = _trailingLearningData[security];

                // Проверяем, достаточно ли данных для обучения
                if (learningData.TotalTrades < SelfLearningMinTrades.ValueInt)
                {
                    // Используем базовое расстояние до накопления достаточной статистики
                    return SelfLearningBaseDistance.ValueDecimal;
                }

                // Получаем текущий анализ тренда
                var trendAnalysis = GetTrendAnalysis(security);

                // Расчет текущей прибыли позиции
                decimal currentProfit = 0;
                if (isLong)
                {
                    currentProfit = (currentPrice - history.EntryPrice) / history.EntryPrice * 100;
                }
                else
                {
                    currentProfit = (history.EntryPrice - currentPrice) / history.EntryPrice * 100;
                }

                // Расчет оптимального расстояния на основе обученных параметров
                decimal optimalDistance = learningData.CalculateOptimalDistance(
                    trendAnalysis.VolatilityPercent,
                    trendAnalysis.TrendStrength,
                    currentProfit);

                // Применяем скорость адаптации
                decimal adaptiveDistance = SelfLearningBaseDistance.ValueDecimal +
                    (optimalDistance - SelfLearningBaseDistance.ValueDecimal) * SelfLearningAdaptationRate.ValueDecimal;

                return Math.Max(0.1m, Math.Min(20.0m, adaptiveDistance));
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка расчета самообучаемого расстояния: {ex.Message}", LogMessageType.Error);
                return SelfLearningBaseDistance.ValueDecimal;
            }
        }

        /// <summary>
        /// Проверка и обновление уровня трейлинг-стопа
        /// </summary>
        private void CheckAndUpdateTrailing(int positionId, decimal currentPrice, bool isLong)
        {
            try
            {
                // Обновляем экстремумы
                UpdatePositionExtremes(positionId, currentPrice, isLong);

                // Получаем текущий уровень трейлинга
                decimal currentTrailingLevel = _currentTrailingLevels.ContainsKey(positionId)
                    ? _currentTrailingLevels[positionId]
                    : (isLong ? decimal.MinValue : decimal.MaxValue);

                // Расчитываем новый уровень
                decimal newTrailingLevel = CalculateTrailingLevel(positionId, currentPrice, isLong);

                // Обновляем уровень только если он улучшается (защита от ухудшения)
                bool shouldUpdate = false;

                if (isLong)
                {
                    // Для LONG: новый уровень должен быть выше текущего
                    shouldUpdate = newTrailingLevel > currentTrailingLevel;
                }
                else
                {
                    // Для SHORT: новый уровень должен быть ниже текущего
                    shouldUpdate = newTrailingLevel < currentTrailingLevel;
                }

                if (shouldUpdate)
                {
                    _currentTrailingLevels[positionId] = newTrailingLevel;
                    SendNewLogMessage($"🔄 Трейлинг-стоп обновлен: позиция {positionId}, уровень {newTrailingLevel:F4}",
                                    LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка обновления трейлинг-стопа: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Обновление экстремумов для позиции
        /// </summary>
        private void UpdatePositionExtremes(int positionId, decimal currentPrice, bool isLong)
        {
            try
            {
                // Обновляем максимум для LONG позиций
                if (isLong)
                {
                    if (!_highestPricesSinceEntry.ContainsKey(positionId) ||
                        currentPrice > _highestPricesSinceEntry[positionId])
                    {
                        _highestPricesSinceEntry[positionId] = currentPrice;
                    }
                }
                // Обновляем минимум для SHORT позиций
                else
                {
                    if (!_lowestPricesSinceEntry.ContainsKey(positionId) ||
                        currentPrice < _lowestPricesSinceEntry[positionId])
                    {
                        _lowestPricesSinceEntry[positionId] = currentPrice;
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка обновления экстремумов: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Получение текущего уровня трейлинг-стопа для позиции
        /// </summary>
        private decimal GetPositionTrailingLevel(int positionId)
        {
            return _currentTrailingLevels.ContainsKey(positionId)
                ? _currentTrailingLevels[positionId]
                : 0;
        }

        /// <summary>
        /// Инициализация трейлинг-стопа при открытии позиции
        /// </summary>
        private void InitializeTrailingStop(Position position)
        {
            try
            {
                if (!UseTrailingStop.ValueBool) return;

                int positionId = position.Number;
                bool isLong = position.Direction == Side.Buy;
                decimal entryPrice = position.EntryPrice;
                string security = position.SecurityName;

                // Инициализируем экстремумы ценой входа
                _highestPricesSinceEntry[positionId] = entryPrice;
                _lowestPricesSinceEntry[positionId] = entryPrice;

                // Создаем историю трейлинга для позиции
                var trendAnalysis = GetTrendAnalysis(security);
                var trailingHistory = new TrailingHistory
                {
                    PositionId = positionId,
                    Security = security,
                    IsLong = isLong,
                    EntryPrice = entryPrice,
                    HighestPrice = entryPrice,
                    LowestPrice = entryPrice,
                    VolatilityAtEntry = trendAnalysis.VolatilityPercent,
                    TrendStrengthAtEntry = trendAnalysis.TrendStrength
                };

                _positionTrailingHistory[positionId] = trailingHistory;

                // Расчитываем начальный уровень трейлинга
                decimal initialTrailingLevel = CalculateTrailingLevel(positionId, entryPrice, isLong);
                _currentTrailingLevels[positionId] = initialTrailingLevel;
                trailingHistory.CurrentTrailingLevel = initialTrailingLevel;

                SendNewLogMessage($"🚀 Трейлинг-стоп инициализирован: позиция {positionId}, уровень {initialTrailingLevel:F4}",
                                LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка инициализации трейлинг-стопа: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Обновление параметров самообучаемого трейлинга при закрытии позиции
        /// </summary>
        private void UpdateSelfLearningParameters(Position position, decimal exitPrice, bool wasTrailingTriggered, string exitReason)
        {
            try
            {
                int positionId = position.Number;
                string security = position.SecurityName;
                bool isLong = position.Direction == Side.Buy;

                // Проверяем, есть ли история для этой позиции
                if (!_positionTrailingHistory.ContainsKey(positionId))
                    return;

                var history = _positionTrailingHistory[positionId];

                // Расчет прибыли в процентах
                decimal profitPercent = isLong
                    ? (exitPrice - history.EntryPrice) / history.EntryPrice * 100
                    : (history.EntryPrice - exitPrice) / history.EntryPrice * 100;

                // Получаем текущий анализ тренда для условий выхода
                var trendAnalysis = GetTrendAnalysis(security);

                // Создаем результат торговли для обучения
                var tradeResult = new TrailingTradeResult
                {
                    ProfitPercent = profitPercent,
                    VolatilityAtExit = trendAnalysis.VolatilityPercent,
                    TrendStrengthAtExit = trendAnalysis.TrendStrength,
                    DistanceAtExit = Math.Abs(exitPrice - (isLong ? history.HighestPrice : history.LowestPrice)) /
                                   (isLong ? history.HighestPrice : history.LowestPrice) * 100,
                    WasTrailingTriggered = wasTrailingTriggered,
                    Direction = isLong ? "Long" : "Short",
                    ExitTime = DateTime.Now
                };

                // Получаем или создаем данные обучения для инструмента
                if (!_trailingLearningData.ContainsKey(security))
                {
                    _trailingLearningData[security] = new TrailingLearningData(security);
                }

                var learningData = _trailingLearningData[security];

                // Обновляем параметры обучения
                learningData.UpdateParameters(tradeResult);

                // Логируем результаты обучения
                SendNewLogMessage($"🧠 Самообучаемый трейлинг обновлен для {security}: " +
                                $"Прибыль {profitPercent:F2}%, Волатильность {trendAnalysis.VolatilityPercent:F2}%, " +
                                $"Сила тренда {trendAnalysis.TrendStrength:F2}, Трейлинг сработал: {wasTrailingTriggered}",
                                LogMessageType.System);

                // Периодически логируем текущие обученные параметры
                if (learningData.TotalTrades % 10 == 0) // Каждые 10 сделок
                {
                    SendNewLogMessage($"📊 Обученные параметры для {security} (сделок: {learningData.TotalTrades}): " +
                                    $"Вес волатильности: {learningData.LearnedParameters["VolatilityWeight"]:F3}, " +
                                    $"Вес тренда: {learningData.LearnedParameters["TrendWeight"]:F3}, " +
                                    $"Базовое расстояние: {learningData.LearnedParameters["BaseDistance"]:F2}%",
                                    LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка обновления параметров самообучаемого трейлинга: {ex.Message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// Очистка данных трейлинг-стопа при закрытии позиции
        /// </summary>
        private void ClearTrailingStopData(int positionId)
        {
            try
            {
                if (_currentTrailingLevels.ContainsKey(positionId))
                    _currentTrailingLevels.Remove(positionId);

                if (_highestPricesSinceEntry.ContainsKey(positionId))
                    _highestPricesSinceEntry.Remove(positionId);

                if (_lowestPricesSinceEntry.ContainsKey(positionId))
                    _lowestPricesSinceEntry.Remove(positionId);

                // Также очищаем историю позиции
                if (_positionTrailingHistory.ContainsKey(positionId))
                    _positionTrailingHistory.Remove(positionId);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка очистки данных трейлинг-стопа: {ex.Message}", LogMessageType.Error);
            }
        }

        #endregion

        #region КЛАССЫ САМООБУЧАЕМОГО ТРЕЙЛИНГА

        /// <summary>
        /// Данные обучения для самообучаемого трейлинга
        /// </summary>
        public class TrailingLearningData
        {
            public string Security { get; set; }
            public List<TrailingTradeResult> TradeHistory { get; set; } = new List<TrailingTradeResult>();
            public Dictionary<string, decimal> LearnedParameters { get; set; } = new Dictionary<string, decimal>();
            public DateTime LastUpdate { get; set; }
            public int TotalTrades { get; set; }
            public decimal AverageProfit { get; set; }
            public decimal WinRate { get; set; }

            public TrailingLearningData(string security)
            {
                Security = security;
                LastUpdate = DateTime.Now;

                // Инициализация базовых параметров обучения
                LearnedParameters["VolatilityWeight"] = 0.4m;
                LearnedParameters["TrendWeight"] = 0.3m;
                LearnedParameters["ProfitWeight"] = 0.3m;
                LearnedParameters["BaseDistance"] = 2.0m;
                LearnedParameters["AdaptiveRate"] = 0.1m;
            }

            /// <summary>
            /// Обновление параметров на основе результатов торговли
            /// </summary>
            public void UpdateParameters(TrailingTradeResult result)
            {
                TradeHistory.Add(result);
                TotalTrades++;
                LastUpdate = DateTime.Now;

                // Поддерживаем историю последних 50 сделок
                if (TradeHistory.Count > 50)
                {
                    TradeHistory.RemoveAt(0);
                }

                // Пересчитываем средние показатели
                RecalculateMetrics();

                // Адаптация параметров на основе результатов
                AdaptParameters(result);
            }

            private void RecalculateMetrics()
            {
                if (TradeHistory.Count == 0) return;

                AverageProfit = TradeHistory.Average(t => t.ProfitPercent);
                WinRate = (decimal)TradeHistory.Count(t => t.ProfitPercent > 0) / TradeHistory.Count * 100;
            }

            private void AdaptParameters(TrailingTradeResult result)
            {
                decimal adaptationRate = LearnedParameters["AdaptiveRate"];

                // Адаптация веса волатильности
                if (result.VolatilityAtExit > 5.0m && result.ProfitPercent < 0)
                {
                    // При высокой волатильности и убытке - увеличиваем вес волатильности
                    LearnedParameters["VolatilityWeight"] = Math.Min(0.8m,
                        LearnedParameters["VolatilityWeight"] + adaptationRate * 0.1m);
                }
                else if (result.VolatilityAtExit < 2.0m && result.ProfitPercent > 0)
                {
                    // При низкой волатильности и прибыли - уменьшаем вес волатильности
                    LearnedParameters["VolatilityWeight"] = Math.Max(0.1m,
                        LearnedParameters["VolatilityWeight"] - adaptationRate * 0.05m);
                }

                // Адаптация веса тренда
                if (result.TrendStrengthAtExit > 60 && result.ProfitPercent > 0)
                {
                    // Сильный тренд и прибыль - увеличиваем вес тренда
                    LearnedParameters["TrendWeight"] = Math.Min(0.8m,
                        LearnedParameters["TrendWeight"] + adaptationRate * 0.1m);
                }
                else if (result.TrendStrengthAtExit < 30 && result.ProfitPercent < 0)
                {
                    // Слабый тренд и убыток - уменьшаем вес тренда
                    LearnedParameters["TrendWeight"] = Math.Max(0.1m,
                        LearnedParameters["TrendWeight"] - adaptationRate * 0.05m);
                }

                // Адаптация базового расстояния
                if (result.ProfitPercent > 2.0m)
                {
                    // Хорошая прибыль - можно увеличить расстояние
                    LearnedParameters["BaseDistance"] = Math.Min(10.0m,
                        LearnedParameters["BaseDistance"] + adaptationRate * 0.2m);
                }
                else if (result.ProfitPercent < -1.0m)
                {
                    // Большой убыток - уменьшаем расстояние
                    LearnedParameters["BaseDistance"] = Math.Max(0.5m,
                        LearnedParameters["BaseDistance"] - adaptationRate * 0.3m);
                }

                // Нормализация весов (должны суммироваться до 1)
                decimal totalWeight = LearnedParameters["VolatilityWeight"] +
                                    LearnedParameters["TrendWeight"] +
                                    LearnedParameters["ProfitWeight"];

                if (totalWeight > 0)
                {
                    LearnedParameters["VolatilityWeight"] /= totalWeight;
                    LearnedParameters["TrendWeight"] /= totalWeight;
                    LearnedParameters["ProfitWeight"] = 1.0m -
                        LearnedParameters["VolatilityWeight"] -
                        LearnedParameters["TrendWeight"];
                }
            }

            /// <summary>
            /// Расчет оптимального расстояния трейлинга на основе текущих условий
            /// </summary>
            public decimal CalculateOptimalDistance(decimal volatility, decimal trendStrength, decimal currentProfit)
            {
                decimal volatilityComponent = volatility * LearnedParameters["VolatilityWeight"];
                decimal trendComponent = trendStrength * LearnedParameters["TrendWeight"] / 100;
                decimal profitComponent = Math.Max(0, currentProfit) * LearnedParameters["ProfitWeight"];

                decimal optimalDistance = LearnedParameters["BaseDistance"] +
                                        volatilityComponent +
                                        trendComponent +
                                        profitComponent;

                return Math.Max(0.1m, Math.Min(20.0m, optimalDistance));
            }
        }

        /// <summary>
        /// Результат торговли для обучения
        /// </summary>
        public class TrailingTradeResult
        {
            public decimal ProfitPercent { get; set; }
            public decimal VolatilityAtExit { get; set; }
            public decimal TrendStrengthAtExit { get; set; }
            public decimal DistanceAtExit { get; set; }
            public bool WasTrailingTriggered { get; set; }
            public string Direction { get; set; } // "Long" или "Short"
            public DateTime ExitTime { get; set; }
        }

        /// <summary>
        /// История трейлинга для позиции
        /// </summary>
        public class TrailingHistory
        {
            public int PositionId { get; set; }
            public string Security { get; set; }
            public bool IsLong { get; set; }
            public decimal EntryPrice { get; set; }
            public decimal HighestPrice { get; set; }
            public decimal LowestPrice { get; set; }
            public decimal CurrentTrailingLevel { get; set; }
            public decimal VolatilityAtEntry { get; set; }
            public decimal TrendStrengthAtEntry { get; set; }
            public List<TrailingUpdate> Updates { get; set; } = new List<TrailingUpdate>();
        }

        /// <summary>
        /// Обновление уровня трейлинга
        /// </summary>
        public class TrailingUpdate
        {
            public DateTime Time { get; set; }
            public decimal Price { get; set; }
            public decimal NewLevel { get; set; }
            public string Reason { get; set; }
        }

        #endregion

        #region БЕЗУБЫТОЧНОЕ ЗАКРЫТИЕ ПОЗИЦИЙ

        /// <summary>
        /// Структура для хранения информации о выходе из позиции
        /// </summary>
        public class ExitInfo
        {
            public decimal Price { get; set; }
            public string Source { get; set; }
        }

        /// <summary>
        /// Компонент управления рисками для безубыточного закрытия
        /// </summary>
        public class RiskManagementComponent
        {
            private readonly UniversalScreenerEngine _robot;
            private readonly Dictionary<int, decimal> _entryPrices = new Dictionary<int, decimal>();
            private readonly Dictionary<int, bool> _wentPositive;

            public RiskManagementComponent(UniversalScreenerEngine robot, Dictionary<int, bool> wentPositive)
            {
                _robot = robot;
                _wentPositive = wentPositive;
            }

            /// <summary>
            /// Регистрация позиции при открытии
            /// </summary>
            public void RegisterPosition(int positionId, decimal entryPrice)
            {
                _entryPrices[positionId] = entryPrice;
                if (!_wentPositive.ContainsKey(positionId))
                {
                    _wentPositive[positionId] = false;
                }
            }

            /// <summary>
            /// Обновление статуса позиции (была ли в плюсе)
            /// </summary>
            public void UpdatePositionStatus(int positionId, decimal currentPrice, bool isLong)
            {
                if (!_entryPrices.ContainsKey(positionId)) return;

                decimal entryPrice = _entryPrices[positionId];
                decimal currentProfitPercent = isLong
                    ? ((currentPrice - entryPrice) / entryPrice) * 100m
                    : ((entryPrice - currentPrice) / entryPrice) * 100m;

                if (currentProfitPercent > 0 && !_wentPositive[positionId])
                {
                    _wentPositive[positionId] = true;
                    _robot.SendNewLogMessage($"🎯 Позиция {positionId} впервые вышла в плюс", LogMessageType.System);
                }
            }

            /// <summary>
            /// Получение минимальной цены прибыли для позиции
            /// </summary>
            public decimal GetMinProfitPrice(int positionId)
            {
                if (!_entryPrices.ContainsKey(positionId)) return 0;

                decimal entryPrice = _entryPrices[positionId];
                decimal minProfitPercent = _robot.MinProfitPercent.ValueDecimal;
                decimal minProfitMultiplier = minProfitPercent / 100m;

                return entryPrice * (1 + minProfitMultiplier); // Для LONG
            }

            /// <summary>
            /// Проверка возможности закрытия позиции
            /// </summary>
            public bool CanClosePosition(int positionId, decimal currentPrice, bool isLong)
            {
                if (!_entryPrices.ContainsKey(positionId)) return true;

                decimal entryPrice = _entryPrices[positionId];
                decimal currentProfitPercent = isLong
                    ? ((currentPrice - entryPrice) / entryPrice) * 100m
                    : ((entryPrice - currentPrice) / entryPrice) * 100m;

                decimal minProfitPercent = _robot.MinProfitPercent.ValueDecimal;

                // КРИТИЧЕСКОЕ УСЛОВИЕ: текущая прибыль < MinProfitPercent
                if (currentProfitPercent < minProfitPercent)
                {
                    // Если позиция никогда не была в плюсе - постоянная блокировка
                    if (!_wentPositive.ContainsKey(positionId) || !_wentPositive[positionId])
                    {
                        // БЛОКИРОВКА: позиция в минусе, никогда не была в плюсе
                        _robot.SendNewLogMessage($"🚫 ЗАКРЫТИЕ ЗАБЛОКИРОВАНО: позиция {positionId} в минусе ({currentProfitPercent:F2}%), никогда не была в плюсе", LogMessageType.System);
                        return false;
                    }

                    // Если позиция была в плюсе, но вернулась в минус - блокировка возобновляется
                    if (currentProfitPercent < 0)
                    {
                        // БЛОКИРОВКА: вернулась в минус после выхода в плюс
                        _robot.SendNewLogMessage($"🚫 ЗАКРЫТИЕ ЗАБЛОКИРОВАНО: позиция {positionId} вернулась в минус ({currentProfitPercent:F2}%) после выхода в плюс", LogMessageType.System);
                        return false;
                    }

                    // Разрешение закрытия по минимальной цене прибыли
                    _robot.SendNewLogMessage($"✅ РАЗРЕШЕНИЕ ЗАКРЫТИЯ: позиция {positionId} может закрыться по минимальной прибыли ({minProfitPercent:F2}%)", LogMessageType.System);
                    return true;
                }

                // Разрешение: текущая прибыль >= MinProfitPercent
                return true;
            }

            /// <summary>
            /// Удаление позиции из отслеживания при закрытии
            /// </summary>
            public void RemovePosition(int positionId)
            {
                if (_entryPrices.ContainsKey(positionId))
                    _entryPrices.Remove(positionId);
                if (_wentPositive.ContainsKey(positionId))
                    _wentPositive.Remove(positionId);
            }
        }

        /// <summary>
        /// Расчет ожидаемой цены выхода из позиции
        /// </summary>
        private ExitInfo GetExpectedExitPrice(Position pos, RiskManagementComponent riskManager,
            decimal entryPrice, decimal currentPrice = 0m)
        {
            decimal minProfitPrice = riskManager?.GetMinProfitPrice(pos.Number) ?? 0m;
            bool isLong = pos.Direction == Side.Buy;

            // Цена минимальной прибыли
            decimal minProfitPercent = MinProfitPercent.ValueDecimal;
            decimal minProfitPriceCalculated = isLong
                ? entryPrice * (1 + minProfitPercent / 100m)
                : entryPrice * (1 - minProfitPercent / 100m);

            // Используем рассчитанную цену минимальной прибыли
            if (minProfitPrice == 0)
                minProfitPrice = minProfitPriceCalculated;

            // Приоритет 1: активный трейлинг
            if (UseTrailingStop.ValueBool && _currentTrailingLevels.ContainsKey(pos.Number))
            {
                decimal trailingLevel = _currentTrailingLevels[pos.Number];
                decimal finalPrice = isLong
                    ? Math.Max(currentPrice, Math.Min(trailingLevel, minProfitPrice))  // Не ниже минимальной прибыли
                    : Math.Min(currentPrice, Math.Max(trailingLevel, minProfitPrice));
                return new ExitInfo { Price = finalPrice, Source = "trailing (current>=min-profit)" };
            }

            // Приоритет 2: ручной тейк-профит
            if (_positionTakeProfits.ContainsKey($"{pos.SecurityName}_{pos.Number}"))
            {
                decimal manualTp = _positionTakeProfits[$"{pos.SecurityName}_{pos.Number}"];
                decimal finalPrice = isLong
                    ? Math.Max(currentPrice, Math.Min(manualTp, minProfitPrice))
                    : Math.Min(currentPrice, Math.Max(manualTp, minProfitPrice));
                return new ExitInfo { Price = finalPrice, Source = "take-profit (current>=min-profit)" };
            }

            // Приоритет 3: минимальная прибыль
            if (minProfitPrice > 0)
            {
                if (currentPrice > 0)
                {
                    decimal finalPrice = isLong
                        ? Math.Max(currentPrice, minProfitPrice)
                        : Math.Min(currentPrice, minProfitPrice);
                    return new ExitInfo { Price = finalPrice, Source = "min-profit (current>=min-profit)" };
                }
                return new ExitInfo { Price = minProfitPrice, Source = "min-profit" };
            }

            return new ExitInfo { Price = entryPrice, Source = "entry" };
        }

        /// <summary>
        /// Попытка закрытия позиции с проверкой риск-менеджмента
        /// </summary>
        private void TryClosePosition(Position position, decimal currentPrice, string reason)
        {
            // Инициализируем риск-менеджер если еще не инициализирован
            if (_riskManager == null)
            {
                _riskManager = new RiskManagementComponent(this, _wentPositive);
            }

            // Проверяем возможность закрытия
            if (!_riskManager.CanClosePosition(position.Number, currentPrice, position.Direction == Side.Buy))
            {
                SendNewLogMessage($"🚫 Попытка закрытия {position.Number} заблокирована риск-менеджментом", LogMessageType.System);
                return; // Закрытие заблокировано
            }

            // Расчет целевой цены через GetExpectedExitPrice
            ExitInfo exitInfo = GetExpectedExitPrice(position, _riskManager, position.EntryPrice, currentPrice);
            decimal closePrice = exitInfo.Price;

            // Закрытие ТОЛЬКО через CloseAtLimit с гарантированной ценой >= minProfitPrice
            try
            {
                var tab = GetTabBySecurityName(position.SecurityName);
                if (tab != null)
                {
                    tab.CloseAtLimit(position, closePrice, position.OpenVolume);
                    SendNewLogMessage($"✅ ЗАКРЫТИЕ ПОЗИЦИИ {position.Number} по цене {closePrice:F4} ({exitInfo.Source})", LogMessageType.Trade);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка закрытия позиции {position.Number}: {ex.Message}", LogMessageType.Error);
            }
        }

        #endregion
    }
}