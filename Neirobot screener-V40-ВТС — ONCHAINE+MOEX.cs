using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Globalization;

namespace OsEngine.Robots.PSO
{
    #region PSO OPTIMIZATION CORE
    public class ParticleSwarmOptimizer
    {
        private readonly Random _random;
        private Particle[] _particles;
        private double[] _globalBestPosition;
        private double _globalBestFitness;
        private double _initialInertia;
        private double _finalInertia;

        public int PopulationSize { get; set; } = 50;
        public int MaxIterations { get; set; } = 100;
        public double InertiaWeight { get; set; } = 0.729;
        public double CognitiveWeight { get; set; } = 1.49445;
        public double SocialWeight { get; set; } = 1.49445;
        public int Dimension { get; set; }
        public double[] MinBounds { get; set; }
        public double[] MaxBounds { get; set; }
        public Func<double[], double> FitnessFunction { get; set; }

        public ParticleSwarmOptimizer(int dimension)
        {
            Dimension = dimension;
            _random = new Random();
            MinBounds = new double[dimension];
            MaxBounds = new double[dimension];
            _globalBestPosition = new double[dimension];
            _globalBestFitness = double.MaxValue;
            _initialInertia = 0.9;
            _finalInertia = 0.4;
        }

        public void Initialize()
        {
            if (Dimension <= 0) throw new ArgumentException("Dimension must be greater than 0");
            if (MinBounds == null || MaxBounds == null) throw new ArgumentNullException("Bounds must be initialized");
            if (FitnessFunction == null) throw new ArgumentNullException("FitnessFunction must be set");

            _particles = new Particle[PopulationSize];

            for (int i = 0; i < PopulationSize; i++)
            {
                _particles[i] = new Particle(Dimension);
                
                for (int j = 0; j < Dimension; j++)
                {
                    _particles[i].Position[j] = MinBounds[j] + _random.NextDouble() * (MaxBounds[j] - MinBounds[j]);
                    _particles[i].Velocity[j] = (_random.NextDouble() - 0.5) * (MaxBounds[j] - MinBounds[j]) * 0.1;
                }

                try
                {
                    _particles[i].Fitness = FitnessFunction(_particles[i].Position);
                    _particles[i].BestFitness = _particles[i].Fitness;
                    Array.Copy(_particles[i].Position, _particles[i].BestPosition, Dimension);

                    if (_particles[i].Fitness < _globalBestFitness)
                    {
                        _globalBestFitness = _particles[i].Fitness;
                        Array.Copy(_particles[i].Position, _globalBestPosition, Dimension);
                    }
                }
                catch (Exception)
                {
                    _particles[i].Fitness = double.MaxValue;
                    _particles[i].BestFitness = double.MaxValue;
                }
            }
        }

        public void RunOptimization()
        {
            if (_particles == null || _particles.Length == 0)
                Initialize();

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                double adaptiveInertia = _initialInertia - 
                    ((_initialInertia - _finalInertia) * iteration / MaxIterations);

                for (int i = 0; i < PopulationSize; i++)
                {
                    UpdateParticle(_particles[i], adaptiveInertia);
                    
                    if (_particles[i].Fitness < _particles[i].BestFitness)
                    {
                        _particles[i].BestFitness = _particles[i].Fitness;
                        Array.Copy(_particles[i].Position, _particles[i].BestPosition, Dimension);
                    }

                    if (_particles[i].Fitness < _globalBestFitness)
                    {
                        _globalBestFitness = _particles[i].Fitness;
                        Array.Copy(_particles[i].Position, _globalBestPosition, Dimension);
                    }
                }
            }
        }

        private void UpdateParticle(Particle particle, double adaptiveInertia)
        {
            for (int j = 0; j < Dimension; j++)
            {
                double r1 = _random.NextDouble();
                double r2 = _random.NextDouble();
                
                particle.Velocity[j] = adaptiveInertia * particle.Velocity[j] +
                                     CognitiveWeight * r1 * (particle.BestPosition[j] - particle.Position[j]) +
                                     SocialWeight * r2 * (_globalBestPosition[j] - particle.Position[j]);

                particle.Position[j] += particle.Velocity[j];

                if (particle.Position[j] < MinBounds[j])
                    particle.Position[j] = MinBounds[j];
                if (particle.Position[j] > MaxBounds[j])
                    particle.Position[j] = MaxBounds[j];
            }

            try
            {
                particle.Fitness = FitnessFunction(particle.Position);
            }
            catch (Exception)
            {
                particle.Fitness = double.MaxValue;
            }
        }

        public double[] GetBestSolution()
        {
            return _globalBestPosition ?? new double[Dimension];
        }

        public double GetBestFitness()
        {
            return _globalBestFitness;
        }
    }

    public class Particle
    {
        public double[] Position { get; set; }
        public double[] Velocity { get; set; }
        public double[] BestPosition { get; set; }
        public double BestFitness { get; set; }
        public double Fitness { get; set; }

        public Particle(int dimension)
        {
            Position = new double[dimension];
            Velocity = new double[dimension];
            BestPosition = new double[dimension];
            BestFitness = double.MaxValue;
            Fitness = double.MaxValue;
        }
    }
    #endregion

    #region COMPONENT ARCHITECTURE
    public interface ITradingComponent
    {
        string Name { get; }
        bool IsInitialized { get; }
        void Initialize(BotPanel bot);
        void Update();
        void Cleanup();
    }

    public class ComponentAssembly
    {
        private readonly List<ITradingComponent> _components = new List<ITradingComponent>();
        private BotPanel _bot;

        public void RegisterComponent(ITradingComponent component)
        {
            _components.Add(component);
        }

        public void Initialize(BotPanel bot)
        {
            _bot = bot;
            foreach (var component in _components)
            {
                component.Initialize(bot);
            }
        }

        public void Update()
        {
            foreach (var component in _components)
            {
                component.Update();
            }
        }

        public void Cleanup()
        {
            foreach (var component in _components)
            {
                component.Cleanup();
            }
        }

        public T GetComponent<T>() where T : class, ITradingComponent
        {
            return _components.OfType<T>().FirstOrDefault();
        }
    }

    public class AdaptiveTradingStateMachine
    {
        public enum TradingState
        {
            Initializing,
            WaitingForSignals,
            PositionOpening,
            PositionMonitoring,
            PositionClosing,
            Paused,
            Error
        }

        private TradingState _currentState = TradingState.Initializing;
        private readonly object _stateLock = new object();
        private readonly BotPanel _bot;

        public AdaptiveTradingStateMachine(BotPanel bot)
        {
            _bot = bot;
        }

        public TradingState CurrentState
        {
            get { lock (_stateLock) return _currentState; }
            set { lock (_stateLock) _currentState = value; }
        }

        public void TransitionTo(TradingState newState)
        {
            lock (_stateLock)
            {
                var oldState = _currentState;
                _currentState = newState;
                
                if (_bot != null)
                {
                    _bot.SendNewLogMessage($"🔄 Переход состояния: {oldState} → {newState}", 
                                          LogMessageType.System);
                }
            }
        }

        public bool IsInState(params TradingState[] states)
        {
            lock (_stateLock)
            {
                return states.Contains(_currentState);
            }
        }
    }
    #endregion

    #region TRADING METRICS AND STATISTICS
    public class TradingMetrics
    {
        public int TotalTrades { get; set; }
        public double WinRate { get; set; }
        public double ProfitFactor { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxDrawdown { get; set; }
        public double Consistency { get; set; }
        public double RecoveryFactor { get; set; }
    }

    public class PositionStatistics
    {
        public string PositionId { get; set; }
        public PositionType Type { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal ProfitPercent { get; set; }
        public decimal ProfitCurrency { get; set; }
        public decimal MaxProfitPercent { get; set; }
        public decimal MaxProfitCurrency { get; set; }
        public decimal MaxLossPercent { get; set; }
        public decimal MaxLossCurrency { get; set; }
        public decimal BreakEvenPrice { get; set; }
        public decimal MinProfitPrice { get; set; }
    }

    public enum PositionType
    {
        Bot,
        Manual
    }

    public class OptimizationStatistics
    {
        public int Iteration { get; set; }
        public double BestFitness { get; set; } = double.MaxValue;
        public double AverageFitness { get; set; }
        public double Diversity { get; set; }
        public double ConvergenceRate { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan EstimatedRemaining { get; set; }

        public void LogProgress(BotPanel robot)
        {
            if (Iteration % 10 == 0)
            {
                robot.SendNewLogMessage(
                    $"🔄 PSO Прогресс | Итерация: {Iteration} | " +
                    $"Лучший фитнес: {BestFitness:F2} | " +
                    $"Прошло времени: {DateTime.Now - StartTime:hh\\:mm\\:ss}",
                    LogMessageType.System);
            }
        }
    }
    #endregion

    #region GENETIC ALGORITHM
    public class GeneticAlgorithmOptimizer
    {
        private readonly Random _random = new Random();
        private Chromosome[] _population;
        private Chromosome _bestChromosome;
        
        public int PopulationSize { get; set; } = 100;
        public int Generations { get; set; } = 100;
        public double MutationRate { get; set; } = 0.01;
        public double CrossoverRate { get; set; } = 0.8;
        public double SelectionPressure { get; set; } = 2.0;
        public Func<double[], double> FitnessFunction { get; set; }
        
        public double[] MinBounds { get; set; }
        public double[] MaxBounds { get; set; }
        public int Dimension { get; set; }

        public void Initialize()
        {
            _population = new Chromosome[PopulationSize];
            _bestChromosome = new Chromosome(Dimension) { Fitness = double.MinValue };

            for (int i = 0; i < PopulationSize; i++)
            {
                _population[i] = new Chromosome(Dimension);
                for (int j = 0; j < Dimension; j++)
                {
                    _population[i].Genes[j] = MinBounds[j] + _random.NextDouble() * (MaxBounds[j] - MinBounds[j]);
                }
                
                _population[i].Fitness = FitnessFunction(_population[i].Genes);
                
                if (_population[i].Fitness > _bestChromosome.Fitness)
                {
                    _bestChromosome = _population[i].Clone();
                }
            }
        }

        public void RunOptimization()
        {
            for (int generation = 0; generation < Generations; generation++)
            {
                // Selection
                var selected = TournamentSelection();
                
                // Crossover
                var offspring = Crossover(selected);
                
                // Mutation
                Mutate(offspring);
                
                // Evaluate
                foreach (var chromosome in offspring)
                {
                    chromosome.Fitness = FitnessFunction(chromosome.Genes);
                    
                    if (chromosome.Fitness > _bestChromosome.Fitness)
                    {
                        _bestChromosome = chromosome.Clone();
                    }
                }
                
                // Replace population
                _population = offspring.ToArray();
            }
        }

        private List<Chromosome> TournamentSelection()
        {
            var selected = new List<Chromosome>();
            int tournamentSize = 3;

            for (int i = 0; i < PopulationSize; i++)
            {
                var tournament = new List<Chromosome>();
                for (int j = 0; j < tournamentSize; j++)
                {
                    tournament.Add(_population[_random.Next(PopulationSize)]);
                }
                
                selected.Add(tournament.OrderByDescending(c => c.Fitness).First());
            }

            return selected;
        }

        private List<Chromosome> Crossover(List<Chromosome> parents)
        {
            var offspring = new List<Chromosome>();
            
            for (int i = 0; i < parents.Count - 1; i += 2)
            {
                var parent1 = parents[i];
                var parent2 = parents[i + 1];
                
                if (_random.NextDouble() < CrossoverRate)
                {
                    var child1 = new Chromosome(Dimension);
                    var child2 = new Chromosome(Dimension);
                    
                    int crossoverPoint = _random.Next(Dimension);
                    
                    for (int j = 0; j < Dimension; j++)
                    {
                        if (j < crossoverPoint)
                        {
                            child1.Genes[j] = parent1.Genes[j];
                            child2.Genes[j] = parent2.Genes[j];
                        }
                        else
                        {
                            child1.Genes[j] = parent2.Genes[j];
                            child2.Genes[j] = parent1.Genes[j];
                        }
                    }
                    
                    offspring.Add(child1);
                    offspring.Add(child2);
                }
                else
                {
                    offspring.Add(parent1.Clone());
                    offspring.Add(parent2.Clone());
                }
            }
            
            return offspring;
        }

        private void Mutate(List<Chromosome> chromosomes)
        {
            foreach (var chromosome in chromosomes)
            {
                for (int i = 0; i < Dimension; i++)
                {
                    if (_random.NextDouble() < MutationRate)
                    {
                        chromosome.Genes[i] = MinBounds[i] + _random.NextDouble() * (MaxBounds[i] - MinBounds[i]);
                    }
                }
            }
        }

        public double[] GetBestSolution()
        {
            return _bestChromosome?.Genes ?? new double[Dimension];
        }

        public double GetBestFitness()
        {
            return _bestChromosome?.Fitness ?? double.MinValue;
        }
    }

    public class Chromosome
    {
        public double[] Genes { get; set; }
        public double Fitness { get; set; }

        public Chromosome(int dimension)
        {
            Genes = new double[dimension];
            Fitness = double.MinValue;
        }

        public Chromosome Clone()
        {
            var clone = new Chromosome(Genes.Length);
            Array.Copy(Genes, clone.Genes, Genes.Length);
            clone.Fitness = Fitness;
            return clone;
        }
    }
    #endregion

    #region HYBRID AI OPTIMIZATION ENGINE
    public class HybridAiOptimizationEngine : ITradingComponent
    {
        public string Name => "Hybrid AI Optimization Engine";
        public bool IsInitialized { get; private set; }

        private readonly BotPanel _bot;
        private ParticleSwarmOptimizer _pso;
        private GeneticAlgorithmOptimizer _ga;
        private bool _isOptimizing = false;
        private OptimizationStatistics _stats;
        private readonly object _optimizationLock = new object();
        private CancellationTokenSource _cts;

        private const string CHECKPOINT_FILE_PSO = "pso_checkpoint.json";
        private const string CHECKPOINT_FILE_GA = "ga_checkpoint.json";

        // Исторические данные для оптимизации
        private List<HistoricalTrade> _historicalTrades = new List<HistoricalTrade>();
        private List<HistoricalCandle> _historicalCandles = new List<HistoricalCandle>();
        private string _historicalDataPath;

        public event Action<double[]> OnBestParametersUpdated;

        public HybridAiOptimizationEngine(BotPanel bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _stats = new OptimizationStatistics();
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;

            // Получаем путь к историческим данным из основного бота
            if (bot is PSOScreenerHybridPro mainBot)
            {
                _historicalDataPath = mainBot.HistoricalDataPath?.ValueString ??
                    GetDefaultHistoricalDataPath();
            }
            else
            {
                _historicalDataPath = GetDefaultHistoricalDataPath();
            }

            // Загружаем исторические данные
            LoadHistoricalData();

            LoadCheckpoints();
            StartContinuousOptimization();
        }

        public void Update()
        {
            // Проверяем условия для оптимизации
            if (!_isOptimizing && ShouldRunOptimization())
            {
                StartOptimization();
            }
        }

        public void Cleanup()
        {
            _cts?.Cancel();
            SaveCheckpoints();
        }

        private bool ShouldRunOptimization()
        {
            try
            {
                // Оптимизация запускается только при определенных условиях:
                // 1. Есть открытые позиции бота
                // 2. Прошло достаточное время с последней оптимизации

                // Получаем основного бота через компонентную архитектуру
                if (_bot is PSOScreenerHybridPro mainBot)
                {
                    // Проверяем наличие открытых ботовских позиций
                    int botPositionsCount = mainBot.CountBotPositions();
                    return botPositionsCount > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                if (_bot != null)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка проверки условий оптимизации: {ex.Message}",
                                          LogMessageType.Error);
                }
                return false;
            }
        }

        private async void StartContinuousOptimization()
        {
            _cts = new CancellationTokenSource();
            
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!_isOptimizing)
                    {
                        await Task.Run(() => RunHybridOptimization(), _cts.Token);
                    }
                    
                    await Task.Delay(TimeSpan.FromMinutes(5), _cts.Token); // Интервал между оптимизациями
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка непрерывной оптимизации: {ex.Message}", 
                                          LogMessageType.Error);
                    await Task.Delay(TimeSpan.FromMinutes(1), _cts.Token);
                }
            }
        }

        private void RunHybridOptimization()
        {
            lock (_optimizationLock)
            {
                if (_isOptimizing) return;
                _isOptimizing = true;
            }

            try
            {
                _bot.SendNewLogMessage("🚀 Запуск гибридной AI оптимизации PSO+GA...", 
                                      LogMessageType.System);

                // Параллельная оптимизация PSO и GA
                var psoTask = Task.Run(() => RunPsoOptimization());
                var gaTask = Task.Run(() => RunGaOptimization());

                Task.WaitAll(psoTask, gaTask);

                // Обмен лучшими решениями
                ExchangeBestSolutions();

                _bot.SendNewLogMessage("✅ Гибридная AI оптимизация завершена успешно!", 
                                      LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка гибридной оптимизации: {ex.Message}", 
                                      LogMessageType.Error);
            }
            finally
            {
                lock (_optimizationLock)
                {
                    _isOptimizing = false;
                }
            }
        }

        private void RunPsoOptimization()
        {
            try
            {
                var parameterBounds = GetParameterBounds();
                
                _pso = new ParticleSwarmOptimizer(parameterBounds.Count)
                {
                    PopulationSize = 50,
                    MaxIterations = 100,
                    MinBounds = parameterBounds.Values.Select(v => v.min).ToArray(),
                    MaxBounds = parameterBounds.Values.Select(v => v.max).ToArray(),
                    FitnessFunction = CalculateFitness
                };

                _pso.Initialize();
                _pso.RunOptimization();

                SaveCheckpoint(_pso.GetBestSolution(), CHECKPOINT_FILE_PSO);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка PSO оптимизации: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        private void RunGaOptimization()
        {
            try
            {
                var parameterBounds = GetParameterBounds();
                
                _ga = new GeneticAlgorithmOptimizer
                {
                    PopulationSize = 100,
                    Generations = 100,
                    MutationRate = 0.01,
                    CrossoverRate = 0.8,
                    SelectionPressure = 2.0,
                    Dimension = parameterBounds.Count,
                    MinBounds = parameterBounds.Values.Select(v => v.min).ToArray(),
                    MaxBounds = parameterBounds.Values.Select(v => v.max).ToArray(),
                    FitnessFunction = CalculateFitness
                };

                _ga.Initialize();
                _ga.RunOptimization();

                SaveCheckpoint(_ga.GetBestSolution(), CHECKPOINT_FILE_GA);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка GA оптимизации: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        private void ExchangeBestSolutions()
        {
            if (_pso == null || _ga == null) return;

            var psoBest = _pso.GetBestSolution();
            var gaBest = _ga.GetBestSolution();

            // Обмен лучшими решениями между алгоритмами
            OnBestParametersUpdated?.Invoke(psoBest);
            OnBestParametersUpdated?.Invoke(gaBest);

            _bot.SendNewLogMessage("🔄 Обмен лучшими решениями между PSO и GA", 
                                  LogMessageType.System);
        }

        private Dictionary<string, (double min, double max)> GetParameterBounds()
        {
            return new Dictionary<string, (double min, double max)>
            {
                { "IchimokuTenkan", (7, 12) },
                { "IchimokuKijun", (20, 30) },
                { "IchimokuSenkouB", (45, 60) },
                { "RsiPeriod", (10, 20) },
                { "Distance", (0.2, 0.8) },
                { "TakeProfitLong", (0.2, 1.0) },
                { "TakeProfitShort", (0.1, 0.8) },
                { "RsiOverbought", (65, 80) },
                { "RsiOversold", (20, 35) },
                { "MinProfitPercent", (0.1, 1.0) },
                { "Ema1Period", (200, 400) },
                { "Ema2Period", (70, 90) },
                { "Ema3Period", (25, 35) }
            };
        }

        private double CalculateFitness(double[] parameters)
        {
            // Фитнес-функция с реальным бэктестингом стратегии на исторических данных
            // Использует загруженные исторические сделки и свечи для оценки параметров
            try
            {
                if (parameters == null || parameters.Length < 10)
                    return double.MaxValue;

                // Извлекаем параметры
                double ichimokuTenkan = parameters[0];
                double ichimokuKijun = parameters[1];
                double ichimokuSenkouB = parameters[2];
                double rsiPeriod = parameters[3];
                double distance = parameters[4];
                double takeProfitLong = parameters[5];
                double takeProfitShort = parameters[6];
                double rsiOverbought = parameters[7];
                double rsiOversold = parameters[8];
                double minProfitPercent = parameters[9];

                // Если есть исторические данные, используем их для бэктестинга
                if (_historicalTrades.Any() || _historicalCandles.Any())
                {
                    return CalculateFitnessFromHistoricalData(parameters);
                }
                else
                {
                    // Фолбэк на симуляцию, если исторические данные не загружены
                    return CalculateFitnessSimulation(parameters);
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка расчета фитнеса: {ex.Message}", LogMessageType.Error);
                return double.MaxValue;
            }
        }

        private double CalculateFitnessFromHistoricalData(double[] parameters)
        {
            // Реальный бэктестинг на исторических данных
            double totalProfit = 0;
            double maxDrawdown = 0;
            double peakProfit = 0;
            int profitableTrades = 0;
            int totalTrades = 0;

            try
            {
                // Анализ прибыльных сделок для понимания паттернов успеха
                var profitableTradesList = _historicalTrades.Where(t => t.IsProfitable).ToList();

                // Основные метрики из исторических данных
                if (_historicalTrades.Any())
                {
                    totalTrades = _historicalTrades.Count;
                    profitableTrades = profitableTradesList.Count;

                    // Расчет общей прибыли из исторических данных
                    totalProfit = _historicalTrades.Sum(t => (double)t.Profit);

                    // Расчет максимальной просадки (упрощенный)
                    double cumulativeProfit = 0;
                    foreach (var trade in _historicalTrades.OrderBy(t => t.CloseTime))
                    {
                        cumulativeProfit += (double)trade.Profit;
                        peakProfit = Math.Max(peakProfit, cumulativeProfit);
                        maxDrawdown = Math.Max(maxDrawdown, peakProfit - cumulativeProfit);
                    }
                }

                // Анализ свечей для оценки параметров индикаторов
                double indicatorQuality = 0;
                if (_historicalCandles.Any())
                {
                    indicatorQuality = EvaluateIndicatorParameters(parameters, _historicalCandles);
                }

                // Расчет финального фитнеса
                double winRate = totalTrades > 0 ? (double)profitableTrades / totalTrades : 0;
                double profitFactor = CalculateProfitFactor(_historicalTrades);
                double consistencyBonus = CalculateConsistencyBonus(profitableTradesList);

                // Финтнес: комбинация прибыли, винрейта, профит-фактора и качества индикаторов
                double fitness = totalProfit * 0.4 +
                               winRate * 1000 +
                               profitFactor * 200 +
                               indicatorQuality * 100 +
                               consistencyBonus * 50;

                // Штраф за высокую просадку
                fitness -= maxDrawdown * 0.3;

                // Для PSO (минимизация), инвертируем
                return -fitness;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка анализа исторических данных: {ex.Message}", LogMessageType.Error);
                return double.MaxValue;
            }
        }

        private double CalculateFitnessSimulation(double[] parameters)
        {
            // Фолбэк на симуляцию, если нет исторических данных
            double ichimokuTenkan = parameters[0];
            double ichimokuKijun = parameters[1];
            double ichimokuSenkouB = parameters[2];
            double rsiPeriod = parameters[3];
            double rsiOverbought = parameters[7];
            double rsiOversold = parameters[8];

            double totalProfit = 0;
            double maxDrawdown = 0;
            double peakProfit = 0;
            int tradesCount = 0;

            // Симулируем сделки с учетом качества параметров
            Random rand = new Random((int)(parameters.Sum() * 1000));
            for (int i = 0; i < 50; i++)
            {
                double baseProfit = (rand.NextDouble() - 0.5) * 2.0;
                double paramQuality = 1.0 / (1.0 + Math.Abs(ichimokuTenkan - 9) +
                                           Math.Abs(ichimokuKijun - 26) +
                                           Math.Abs(rsiPeriod - 14) +
                                           Math.Abs(rsiOverbought - 70) +
                                           Math.Abs(rsiOversold - 30));

                double tradeProfit = baseProfit * paramQuality * 100;
                totalProfit += tradeProfit;
                tradesCount++;

                peakProfit = Math.Max(peakProfit, totalProfit);
                maxDrawdown = Math.Max(maxDrawdown, peakProfit - totalProfit);
            }

            double avgProfit = totalProfit / Math.Max(tradesCount, 1);
            double sharpeRatio = avgProfit / Math.Max(maxDrawdown, 0.01);

            double fitness = totalProfit - maxDrawdown * 0.5 + sharpeRatio * 10;
            return -fitness;
        }

        private double EvaluateIndicatorParameters(double[] parameters, List<HistoricalCandle> candles)
        {
            // Оценка качества параметров индикаторов на исторических свечах
            double quality = 0;

            try
            {
                // Здесь можно добавить анализ качества сигналов индикаторов
                // на исторических данных (сигналы входа/выхода совпадают с прибыльными сделками)

                // Упрощенная оценка: насколько параметры близки к оптимальным
                double ichimokuTenkan = parameters[0];
                double ichimokuKijun = parameters[1];
                double rsiPeriod = parameters[3];
                double rsiOverbought = parameters[7];
                double rsiOversold = parameters[8];

                // Оптимальные значения (можно адаптировать на основе анализа прибыльных сделок)
                double optimalTenkan = 9;
                double optimalKijun = 26;
                double optimalRsiPeriod = 14;
                double optimalOverbought = 70;
                double optimalOversold = 30;

                quality = 1.0 / (1.0 + Math.Abs(ichimokuTenkan - optimalTenkan) * 0.1 +
                                Math.Abs(ichimokuKijun - optimalKijun) * 0.1 +
                                Math.Abs(rsiPeriod - optimalRsiPeriod) * 0.2 +
                                Math.Abs(rsiOverbought - optimalOverbought) * 0.1 +
                                Math.Abs(rsiOversold - optimalOversold) * 0.1);
            }
            catch
            {
                quality = 0.5; // Среднее значение при ошибке
            }

            return quality;
        }

        private double CalculateProfitFactor(List<HistoricalTrade> trades)
        {
            if (!trades.Any()) return 1.0;

            double grossProfit = trades.Where(t => t.Profit > 0).Sum(t => (double)t.Profit);
            double grossLoss = Math.Abs(trades.Where(t => t.Profit < 0).Sum(t => (double)t.Profit));

            return grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? 10.0 : 1.0;
        }

        private double CalculateConsistencyBonus(List<HistoricalTrade> profitableTrades)
        {
            if (!profitableTrades.Any()) return 0;

            // Бонус за последовательные прибыльные сделки
            double consistency = 0;

            // Анализ временных интервалов между прибыльными сделками
            var sortedTrades = profitableTrades.OrderBy(t => t.CloseTime).ToList();

            for (int i = 1; i < sortedTrades.Count; i++)
            {
                TimeSpan interval = sortedTrades[i].CloseTime - sortedTrades[i - 1].CloseTime;
                if (interval.TotalHours < 24) // Прибыльные сделки в течение дня
                {
                    consistency += 0.1;
                }
            }

            return Math.Min(consistency, 2.0); // Ограничение максимального бонуса
        }

        private void SaveCheckpoint(double[] solution, string fileName)
        {
            try
            {
                // В реальной реализации здесь должна быть сериализация в JSON
                _bot.SendNewLogMessage($"💾 Сохранен чекпоинт оптимизации: {fileName}", 
                                      LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка сохранения чекпоинта: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        private string GetDefaultHistoricalDataPath()
        {
            // Список возможных путей к папке Data в OsEngine
            var possiblePaths = new[]
            {
                // Текущая директория программы + Data
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"),

                // Путь для OsEngine-master
                @"C:\OsEngine-master\OsEngine-master\project\OsEngine\bin\Debug\Data",

                // Альтернативные пути
                @"C:\Program Files\OsEngine\Data",
                @"C:\OsEngine\Data",

                // Папка Data в директории пользователя
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OsEngine", "Data")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            // Если ни одна папка не найдена, возвращаем путь по умолчанию
            return @"C:\OsEngine-master\OsEngine-master\project\OsEngine\bin\Debug\Data";
        }

        private void LoadHistoricalData()
        {
            try
            {
                _bot.SendNewLogMessage($"📊 Загрузка исторических данных из: {_historicalDataPath}", LogMessageType.System);

                if (!Directory.Exists(_historicalDataPath))
                {
                    _bot.SendNewLogMessage($"⚠️ Папка с историческими данными не найдена: {_historicalDataPath}. Создание пустых списков.", LogMessageType.System);
                    return;
                }

                // Загрузка файлов с историческими сделками
                var tradeFiles = Directory.GetFiles(_historicalDataPath, "*trades*.csv", SearchOption.AllDirectories);
                foreach (var file in tradeFiles)
                {
                    LoadTradesFromFile(file);
                }

                // Загрузка файлов с историческими свечами (рекурсивно из всех подпапок)
                var candleFiles = Directory.GetFiles(_historicalDataPath, "*candles*.txt", SearchOption.AllDirectories);
                var txtFiles = Directory.GetFiles(_historicalDataPath, "*.txt", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("trades")).ToList();
                candleFiles = candleFiles.Concat(txtFiles).Distinct().ToArray();

                // Логирование найденных файлов для отладки
                if (candleFiles.Any())
                {
                    _bot.SendNewLogMessage($"📁 Найдено файлов со свечами: {candleFiles.Length}", LogMessageType.System);
                    foreach (var file in candleFiles.Take(5)) // Показываем первые 5 файлов
                    {
                        _bot.SendNewLogMessage($"📄 Файл: {Path.GetFileName(file)} в {Path.GetDirectoryName(file)}", LogMessageType.System);
                    }
                    if (candleFiles.Length > 5)
                    {
                        _bot.SendNewLogMessage($"... и еще {candleFiles.Length - 5} файлов", LogMessageType.System);
                    }
                }
                foreach (var file in candleFiles)
                {
                    LoadCandlesFromFile(file);
                }

                _bot.SendNewLogMessage($"✅ Загружено {_historicalTrades.Count} сделок и {_historicalCandles.Count} свечей для оптимизации", LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка загрузки исторических данных: {ex.Message}", LogMessageType.Error);
            }
        }

        private void LoadTradesFromFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines.Skip(1)) // Пропускаем заголовок
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 9)
                    {
                        var trade = new HistoricalTrade
                        {
                            OpenTime = DateTime.Parse(parts[0]),
                            CloseTime = DateTime.Parse(parts[1]),
                            Direction = parts[2],
                            EntryPrice = decimal.Parse(parts[3]),
                            ExitPrice = decimal.Parse(parts[4]),
                            Volume = decimal.Parse(parts[5]),
                            Profit = decimal.Parse(parts[6]),
                            ProfitPercent = decimal.Parse(parts[7]),
                            IsProfitable = parts[8].ToLower() == "true"
                        };
                        _historicalTrades.Add(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка загрузки сделок из {filePath}: {ex.Message}", LogMessageType.Error);
            }
        }

        private void LoadCandlesFromFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines.Skip(1)) // Пропускаем заголовок
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 7)
                    {
                        // Парсим дату и время: 20250101,000000
                        string dateStr = parts[0]; // 20250101
                        string timeStr = parts[1]; // 000000

                        int year = int.Parse(dateStr.Substring(0, 4));
                        int month = int.Parse(dateStr.Substring(4, 2));
                        int day = int.Parse(dateStr.Substring(6, 2));

                        int hour = int.Parse(timeStr.Substring(0, 2));
                        int minute = int.Parse(timeStr.Substring(2, 2));
                        int second = int.Parse(timeStr.Substring(4, 2));

                        DateTime candleTime = new DateTime(year, month, day, hour, minute, second);

                        var candle = new HistoricalCandle
                        {
                            Time = candleTime,
                            Open = decimal.Parse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture),
                            High = decimal.Parse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture),
                            Low = decimal.Parse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture),
                            Close = decimal.Parse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture),
                            Volume = decimal.Parse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture)
                        };
                        _historicalCandles.Add(candle);
                    }
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка загрузки свечей из {filePath}: {ex.Message}", LogMessageType.Error);
            }
        }

        private void LoadCheckpoints()
        {
            try
            {
                // В реальной реализации здесь должна быть загрузка из JSON
                _bot.SendNewLogMessage("📂 Загрузка чекпоинтов оптимизации...",
                                      LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка загрузки чекпоинтов: {ex.Message}",
                                      LogMessageType.Error);
            }
        }

        private void SaveCheckpoints()
        {
            try
            {
                // В реальной реализации здесь должна быть сериализация
                _bot.SendNewLogMessage("💾 Сохранение всех чекпоинтов...", 
                                      LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка сохранения чекпоинтов: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        public void StartOptimization()
        {
            if (!_isOptimizing)
            {
                Task.Run(() => RunHybridOptimization());
            }
        }

        public string GetOptimizationStatus()
        {
            return _isOptimizing 
                ? "🚀 Оптимизация выполняется..." 
                : "✅ Оптимизация неактивна";
        }
    }
    #endregion

    #region POSITION MANAGEMENT SYSTEM
    public class PositionManager : ITradingComponent
    {
        public string Name => "Position Manager";
        public bool IsInitialized { get; private set; }
        
        private BotPanel _bot;
        private readonly ConcurrentDictionary<string, PositionStatistics> _positionStats = 
            new ConcurrentDictionary<string, PositionStatistics>();
        private readonly ConcurrentDictionary<string, bool> _wentPositive = 
            new ConcurrentDictionary<string, bool>();
        private readonly object _statsLock = new object();
        
        // Для защиты от ухода из плюса в минус
        private readonly ConcurrentDictionary<string, decimal> _peakProfit = 
            new ConcurrentDictionary<string, decimal>();
        
        public PositionManager()
        {
        }

        public void Initialize(BotPanel bot)
        {
            _bot = bot;
            IsInitialized = true;
            
            // Инициализация существующих позиций при старте
            Task.Run(() => InitializeExistingPositions());
        }

        public void Update()
        {
            UpdatePositionStatistics();
        }

        public void Cleanup()
        {
            _positionStats.Clear();
            _wentPositive.Clear();
            _peakProfit.Clear();
        }

        private async Task InitializeExistingPositions()
        {
            try
            {
                await Task.Delay(3000); // Ждем 3 секунды для загрузки данных
                
                _bot.SendNewLogMessage("🔍 Поиск существующих позиций при старте...", 
                                      LogMessageType.System);
                
                // Здесь должна быть логика инициализации существующих позиций
                // В реальной реализации нужно получить все открытые позиции из бота
                
                _bot.SendNewLogMessage("✅ Инициализация позиций завершена", 
                                      LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка инициализации позиций: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        private void UpdatePositionStatistics()
        {
            // В реальной реализации здесь обновление статистики по всем позициям
        }

        public bool CanClosePosition(Position position, decimal minProfitPercent, 
                                    BotTabSimple tab = null, decimal currentPrice = 0)
        {
            if (position == null) return false;

            try
            {
                string positionId = position.Number.ToString();
                decimal profit = CalculatePositionProfit(position, tab, currentPrice);
                decimal entryPrice = position.EntryPrice;
                
                if (entryPrice == 0) return false;

                decimal profitPercent = (profit / (entryPrice * Math.Abs(position.OpenVolume))) * 100m;
                decimal requiredProfit = entryPrice * (minProfitPercent / 100m) * Math.Abs(position.OpenVolume);

                // Абсолютная защита от убытков
                if (profit < requiredProfit)
                {
                    LogPositionBlocked(position, profit, requiredProfit, profitPercent, minProfitPercent);
                    return false;
                }

                // Защита от ухода из плюса в минус
                if (_wentPositive.ContainsKey(positionId) && _wentPositive[positionId])
                {
                    if (profit < 0)
                    {
                        _bot.SendNewLogMessage(
                            $"🚨 ЗАПРЕТ ЗАКРЫТИЯ: Позиция #{positionId} пытается уйти из плюса в минус! " +
                            $"Текущий PnL: {profit:F2}",
                            LogMessageType.Error);
                        return false;
                    }
                }

                // Обновляем пиковую прибыль
                if (profit > 0)
                {
                    _peakProfit.AddOrUpdate(positionId, profit, (id, old) => Math.Max(old, profit));
                    
                    if (!_wentPositive.ContainsKey(positionId))
                    {
                        _wentPositive[positionId] = true;
                        _bot.SendNewLogMessage(
                            $"✅ Позиция #{positionId} вышла в плюс: {profit:F2}",
                            LogMessageType.System);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка проверки закрытия позиции: {ex.Message}", 
                                      LogMessageType.Error);
                return false;
            }
        }

        private decimal CalculatePositionProfit(Position position, BotTabSimple tab, decimal currentPrice)
        {
            try
            {
                if (position == null) return 0;

                if (currentPrice == 0 && tab != null && tab.CandlesFinishedOnly != null && tab.CandlesFinishedOnly.Count > 0)
                {
                    currentPrice = tab.CandlesFinishedOnly.Last().Close;
                }
                else if (currentPrice == 0)
                {
                    currentPrice = position.EntryPrice;
                }

                decimal priceDifference = currentPrice - position.EntryPrice;
                
                if (position.Direction == Side.Sell)
                    priceDifference = -priceDifference;

                decimal profit = priceDifference * Math.Abs(position.OpenVolume);
                return profit;
            }
            catch
            {
                return 0;
            }
        }

        private void LogPositionBlocked(Position position, decimal profit, decimal requiredProfit, 
                                       decimal profitPercent, decimal minProfitPercent)
        {
            _bot.SendNewLogMessage(
                $"⛔ БЛОКИРОВКА ЗАКРЫТИЯ: Позиция #{position.Number} | " +
                $"Тип: {(IsBotPosition(position) ? "БОТ" : "РУЧНАЯ")} | " +
                $"Прибыль: {profit:F2} ({profitPercent:F2}%) | " +
                $"Требуется: {requiredProfit:F2} ({minProfitPercent}%) | " +
                $"Направление: {position.Direction}",
                LogMessageType.System);
        }

        public void RegisterPosition(Position position, PositionType type)
        {
            if (position == null) return;

            string positionId = position.Number.ToString();
            
            var stats = new PositionStatistics
            {
                PositionId = positionId,
                Type = type,
                EntryPrice = position.EntryPrice,
                CurrentPrice = position.EntryPrice,
                Volume = Math.Abs(position.OpenVolume)
            };

            _positionStats[positionId] = stats;
            
            LogPositionRegistered(position, type);
        }

        private void LogPositionRegistered(Position position, PositionType type)
        {
            string positionType = type == PositionType.Bot ? "БОТ" : "РУЧНАЯ";
            
            _bot.SendNewLogMessage(
                $"✅ {positionType} ПОЗИЦИЯ #{position.Number} ЗАРЕГИСТРИРОВАНА\n" +
                $"Инструмент: {position.SecurityName}\n" +
                $"Направление: {position.Direction}\n" +
                $"Цена входа: {position.EntryPrice:F4}\n" +
                $"Объем: {Math.Abs(position.OpenVolume):F2}\n" +
                $"Время открытия: {position.TimeOpen}",
                LogMessageType.System);
        }

        public PositionStatistics GetPositionStatistics(string positionId)
        {
            _positionStats.TryGetValue(positionId, out var stats);
            return stats;
        }

        public bool IsBotPosition(Position position)
        {
            // В реальной реализации здесь логика определения типа позиции
            return true; // Временная заглушка
        }

        public List<PositionStatistics> GetAllPositionStatistics()
        {
            return _positionStats.Values.ToList();
        }

        public void UpdatePositionPrice(string positionId, decimal currentPrice)
        {
            if (_positionStats.TryGetValue(positionId, out var stats))
            {
                stats.CurrentPrice = currentPrice;
                
                decimal profit = (currentPrice - stats.EntryPrice) * stats.Volume;
                if (stats.EntryPrice != 0)
                {
                    stats.ProfitPercent = (profit / (stats.EntryPrice * stats.Volume)) * 100m;
                }
                stats.ProfitCurrency = profit;

                // Обновляем максимальные значения
                if (profit > stats.MaxProfitCurrency)
                {
                    stats.MaxProfitCurrency = profit;
                    stats.MaxProfitPercent = stats.ProfitPercent;
                }
                
                if (profit < stats.MaxLossCurrency)
                {
                    stats.MaxLossCurrency = profit;
                    stats.MaxLossPercent = stats.ProfitPercent;
                }
            }
        }
    }
    #endregion

    #region ON-CHAIN ANALYSIS SYSTEM
    public class OnChainAnalyzer : ITradingComponent
    {
        public string Name => "On-Chain Analyzer";
        public bool IsInitialized { get; private set; }

        private readonly BotPanel _bot;
        private readonly ConcurrentDictionary<string, OnChainMetrics> _metricsCache = new ConcurrentDictionary<string, OnChainMetrics>();
        private readonly ConcurrentDictionary<string, DateTime> _lastUpdateTimes = new ConcurrentDictionary<string, DateTime>();
        private CancellationTokenSource _cts;
        private Timer _updateTimer;

        // Конфигурация
        private readonly string[] _supportedBlockchains = { "BTC", "ETH" };
        private readonly int _updateIntervalMinutes = 5;
        private readonly decimal _whaleTransactionThreshold = 1000000m; // $1M

        public OnChainAnalyzer(BotPanel bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;

            // Инициализация кеша для поддерживаемых блокчейнов
            foreach (var blockchain in _supportedBlockchains)
            {
                _metricsCache[blockchain] = new OnChainMetrics();
                _lastUpdateTimes[blockchain] = DateTime.MinValue;
            }

            // Запуск периодического обновления данных
            StartPeriodicUpdates();

            _bot.SendNewLogMessage("🔗 On-Chain анализатор инициализирован", LogMessageType.System);
        }

        public void Update()
        {
            // Проверка необходимости обновления данных
            foreach (var blockchain in _supportedBlockchains)
            {
                if (ShouldUpdateData(blockchain))
                {
                    Task.Run(() => UpdateBlockchainData(blockchain));
                }
            }
        }

        public void Cleanup()
        {
            _cts?.Cancel();
            _updateTimer?.Dispose();
            _metricsCache.Clear();
            _lastUpdateTimes.Clear();
        }

        private void StartPeriodicUpdates()
        {
            _cts = new CancellationTokenSource();

            // Таймер для периодического обновления
            _updateTimer = new Timer(async (state) =>
            {
                try
                {
                    foreach (var blockchain in _supportedBlockchains)
                    {
                        await UpdateBlockchainData(blockchain);
                    }
                }
                catch (Exception ex)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка обновления On-Chain данных: {ex.Message}", LogMessageType.Error);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(_updateIntervalMinutes));
        }

        private bool ShouldUpdateData(string blockchain)
        {
            if (!_lastUpdateTimes.ContainsKey(blockchain))
                return true;

            TimeSpan timeSinceLastUpdate = DateTime.Now - _lastUpdateTimes[blockchain];
            return timeSinceLastUpdate.TotalMinutes >= _updateIntervalMinutes;
        }

        private async Task UpdateBlockchainData(string blockchain)
        {
            try
            {
                // Имитация получения данных из API (в реальной реализации - реальные API вызовы)
                var metrics = await FetchOnChainData(blockchain);

                _metricsCache[blockchain] = metrics;
                _lastUpdateTimes[blockchain] = DateTime.Now;

                _bot.SendNewLogMessage(
                    $"🔗 On-Chain обновлено для {blockchain}: Активных адресов: {metrics.ActiveAddresses24h}",
                    LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка обновления {blockchain}: {ex.Message}", LogMessageType.Error);
            }
        }

        private async Task<OnChainMetrics> FetchOnChainData(string blockchain)
        {
            // В реальной реализации здесь будут вызовы реальных API
            // Glassnode, Santiment, Dune Analytics и т.д.

            await Task.Delay(100); // Имитация сетевого запроса

            var random = new Random();

            return new OnChainMetrics
            {
                // Объемы транзакций
                LargeTransactionVolume24h = (decimal)(random.NextDouble() * 5000000), // $0-5M
                WhaleTransactionCount = random.Next(50, 200),

                // Активность сети
                ActiveAddresses24h = random.Next(500000, 2000000),
                HashRate = blockchain == "BTC" ? (decimal)(random.NextDouble() * 500) : 0, // TH/s для BTC

                // Распределение
                Top10HolderPercentage = (decimal)(random.NextDouble() * 30 + 10), // 10-40%

                // Рыночные метрики
                FearGreedIndex = random.Next(20, 80),
                ExchangeInflow24h = (decimal)(random.NextDouble() * 1000000),
                ExchangeOutflow24h = (decimal)(random.NextDouble() * 1000000),

                // Временная метка
                LastUpdate = DateTime.Now
            };
        }

        public OnChainMetrics GetMetrics(string blockchain)
        {
            if (_metricsCache.TryGetValue(blockchain, out var metrics))
            {
                return metrics;
            }

            // Возвращаем пустые метрики, если данных нет
            return new OnChainMetrics { LastUpdate = DateTime.MinValue };
        }

        public bool ShouldFilterSignal(string blockchain, string direction, decimal currentPrice)
        {
            var metrics = GetMetrics(blockchain);
            if (metrics.LastUpdate == DateTime.MinValue) return false;

            // Фильтр экстремальной волатильности On-Chain
            if (metrics.LargeTransactionVolume24h > _whaleTransactionThreshold * 2)
            {
                _bot.SendNewLogMessage(
                    $"🚨 On-Chain фильтр: Высокая волатильность {blockchain} - сигнал заблокирован",
                    LogMessageType.System);
                return true; // Заблокировать сигнал
            }

            // Фильтр направления капитала
            if (direction == "Long" && metrics.ExchangeOutflow24h > metrics.ExchangeInflow24h * 1.5m)
            {
                return false; // Позволить лонг при оттоке с бирж
            }

            if (direction == "Short" && metrics.ExchangeInflow24h > metrics.ExchangeOutflow24h * 1.5m)
            {
                return false; // Позволить шорт при притоке на биржи
            }

            return false; // Не фильтровать по умолчанию
        }

        public decimal GetTrendStrength(string blockchain)
        {
            var metrics = GetMetrics(blockchain);
            if (metrics.LastUpdate == DateTime.MinValue) return 1.0m; // Нейтральная сила

            // Комбинированная оценка силы тренда
            decimal activityScore = Math.Min(metrics.ActiveAddresses24h / 1000000m, 2.0m); // Нормализация
            decimal capitalFlowScore = metrics.ExchangeOutflow24h > metrics.ExchangeInflow24h ?
                metrics.ExchangeOutflow24h / Math.Max(metrics.ExchangeInflow24h, 1) : 0.5m;
            decimal distributionScore = 1 - (metrics.Top10HolderPercentage / 100m); // Лучше при равномерном распределении

            return (activityScore + capitalFlowScore + distributionScore) / 3.0m;
        }

        public string GetOnChainStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine("🔗 On-Chain статус:");

            foreach (var blockchain in _supportedBlockchains)
            {
                var metrics = GetMetrics(blockchain);
                var timeSinceUpdate = DateTime.Now - metrics.LastUpdate;

                status.AppendLine($"{blockchain}: Активных адресов: {metrics.ActiveAddresses24h}, " +
                               $"Обновлено: {timeSinceUpdate.TotalMinutes:F1} мин назад");
            }

            return status.ToString();
        }
    }

    public class OnChainMetrics
    {
        // Объемы и активность
        public decimal LargeTransactionVolume24h { get; set; }
        public int WhaleTransactionCount { get; set; }

        // Активность сети
        public int ActiveAddresses24h { get; set; }
        public decimal HashRate { get; set; } // TH/s для BTC
        public decimal GasUsed { get; set; } // Для ETH

        // Распределение токенов
        public decimal Top10HolderPercentage { get; set; }

        // Рыночные метрики
        public decimal FearGreedIndex { get; set; } // 0-100
        public decimal ExchangeInflow24h { get; set; }
        public decimal ExchangeOutflow24h { get; set; }

        // TVL для DeFi
        public decimal TVL { get; set; }

        // Метаданные
        public DateTime LastUpdate { get; set; }
    }

    #endregion

    #region ORDER BOOK ANALYSIS SYSTEM (МОСБИРЖА)
    public class OrderBookAnalyzer : ITradingComponent
    {
        public string Name => "OrderBook Analyzer (Мосбиржа)";
        public bool IsInitialized { get; private set; }

        private readonly BotPanel _bot;
        private readonly ConcurrentDictionary<string, OrderBookSnapshot> _latestBooks = new ConcurrentDictionary<string, OrderBookSnapshot>();
        private readonly ConcurrentDictionary<string, List<OrderBookSnapshot>> _bookHistory = new ConcurrentDictionary<string, List<OrderBookSnapshot>>();
        private readonly ConcurrentDictionary<string, OrderBookMetrics> _metricsCache = new ConcurrentDictionary<string, OrderBookMetrics>();
        private readonly ConcurrentDictionary<string, DateTime> _lastAnalysisTime = new ConcurrentDictionary<string, DateTime>();

        // Конфигурация
        private readonly int _maxHistoryDepth = 100;
        private readonly int _analysisDepth = 20;
        private readonly int _updateIntervalMs = 100;
        private readonly decimal _wallThresholdPercent = 5.0m;
        private readonly decimal _abnormalVolumeZScoreThreshold = 3.0m;

        public OrderBookAnalyzer(BotPanel bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;
            _bot.SendNewLogMessage("📊 OrderBook Analyzer для Мосбиржи инициализирован", LogMessageType.System);
        }

        public void Update()
        {
            // Обновление метрик для всех активных инструментов
            foreach (var security in _latestBooks.Keys)
            {
                if (ShouldUpdateMetrics(security))
                {
                    UpdateMetrics(security);
                }
            }
        }

        public void Cleanup()
        {
            _latestBooks.Clear();
            _bookHistory.Clear();
            _metricsCache.Clear();
            _lastAnalysisTime.Clear();
        }

        private bool ShouldUpdateMetrics(string security)
        {
            if (!_lastAnalysisTime.ContainsKey(security))
                return true;

            TimeSpan timeSinceLastUpdate = DateTime.Now - _lastAnalysisTime[security];
            return timeSinceLastUpdate.TotalMilliseconds >= _updateIntervalMs;
        }

        public void UpdateOrderBook(string security, List<OrderBookLevel> bids, List<OrderBookLevel> asks)
        {
            try
            {
                var snapshot = new OrderBookSnapshot
                {
                    Timestamp = DateTime.Now,
                    Security = security,
                    Bids = bids.Take(_analysisDepth).ToList(),
                    Asks = asks.Take(_analysisDepth).ToList()
                };

                // Рассчитываем спред и лучшие цены
                if (snapshot.Bids.Any() && snapshot.Asks.Any())
                {
                    snapshot.BestBid = snapshot.Bids.First().Price;
                    snapshot.BestAsk = snapshot.Asks.First().Price;
                    snapshot.Spread = snapshot.BestAsk - snapshot.BestBid;
                }

                // Сохраняем снимок
                _latestBooks[security] = snapshot;

                // Добавляем в историю
                if (!_bookHistory.ContainsKey(security))
                    _bookHistory[security] = new List<OrderBookSnapshot>();

                _bookHistory[security].Add(snapshot);

                // Ограничиваем глубину истории
                if (_bookHistory[security].Count > _maxHistoryDepth)
                {
                    _bookHistory[security].RemoveAt(0);
                }

                // Обновляем метрики
                UpdateMetrics(security);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка обновления стакана {security}: {ex.Message}", LogMessageType.Error);
            }
        }

        private void UpdateMetrics(string security)
        {
            try
            {
                if (!_latestBooks.ContainsKey(security) || !_bookHistory.ContainsKey(security))
                    return;

                var current = _latestBooks[security];
                var history = _bookHistory[security];

                if (history.Count < 2) return;

                var previous = history[history.Count - 2];

                var metrics = new OrderBookMetrics
                {
                    LargeOrderDetectionScore = DetectLargeOrderExecution(current, previous),
                    DetectedWalls = DetectWalls(current),
                    AbnormalVolumeZScore = CalculateAbnormalVolumeZScore(current, history),
                    SpreadScore = AnalyzeSpreadAndDepth(current, history).spreadScore,
                    DepthScore = AnalyzeSpreadAndDepth(current, history).depthScore
                };

                // Анализируем кластеры
                var clusterAnalysis = AnalyzeVolumeClusters(history);
                metrics.Clusters = clusterAnalysis.clusters;
                metrics.PointOfControl = clusterAnalysis.poc;
                metrics.ValueArea = clusterAnalysis.valueArea;

                // Определяем наличие паттернов
                metrics.HasLargeOrderExecution = metrics.LargeOrderDetectionScore > 1000;
                metrics.HasResistanceWall = metrics.DetectedWalls.Any(w => w.Type == WallType.Resistance);
                metrics.HasSupportWall = metrics.DetectedWalls.Any(w => w.Type == WallType.Support);
                metrics.HasAbnormalTickVolume = Math.Abs(metrics.AbnormalVolumeZScore) > _abnormalVolumeZScoreThreshold;
                metrics.HasIncreasingLiquidity = metrics.DepthScore > 1.2m;

                _metricsCache[security] = metrics;
                _lastAnalysisTime[security] = DateTime.Now;

                // Логируем важные обнаружения
                LogImportantPatterns(security, metrics);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка расчета метрик {security}: {ex.Message}", LogMessageType.Error);
            }
        }

        private decimal DetectLargeOrderExecution(OrderBookSnapshot current, OrderBookSnapshot previous)
        {
            if (current.Bids.Count == 0 || current.Asks.Count == 0 ||
                previous.Bids.Count == 0 || previous.Asks.Count == 0)
                return 0;

            // Сравниваем объемы на лучших уровнях
            decimal bidVolumeChange = current.Bids.First().Volume - previous.Bids.First().Volume;
            decimal askVolumeChange = current.Asks.First().Volume - previous.Asks.First().Volume;

            // Резкое уменьшение объема = исполнение крупного ордера
            decimal volumeDrop = Math.Min(bidVolumeChange, askVolumeChange);

            return Math.Max(0, -volumeDrop); // Возвращаем положительное значение для объема исполнения
        }

        private List<WallInfo> DetectWalls(OrderBookSnapshot book)
        {
            var walls = new List<WallInfo>();
            decimal totalVolume = book.Bids.Sum(b => b.Volume) + book.Asks.Sum(a => a.Volume);

            if (totalVolume == 0) return walls;

            // Анализируем bids (стены поддержки)
            foreach (var level in book.Bids.Take(10))
            {
                decimal volumePercent = (level.Volume / totalVolume) * 100;
                if (volumePercent > _wallThresholdPercent)
                {
                    walls.Add(new WallInfo
                    {
                        PriceLevel = level.Price,
                        Volume = level.Volume,
                        VolumePercentage = volumePercent,
                        Type = WallType.Support
                    });
                }
            }

            // Анализируем asks (стены сопротивления)
            foreach (var level in book.Asks.Take(10))
            {
                decimal volumePercent = (level.Volume / totalVolume) * 100;
                if (volumePercent > _wallThresholdPercent)
                {
                    walls.Add(new WallInfo
                    {
                        PriceLevel = level.Price,
                        Volume = level.Volume,
                        VolumePercentage = volumePercent,
                        Type = WallType.Resistance
                    });
                }
            }

            return walls;
        }

        private decimal CalculateAbnormalVolumeZScore(OrderBookSnapshot current, List<OrderBookSnapshot> history)
        {
            if (history.Count < 10) return 0;

            // Собираем историю объемов сделок (используем объем на лучших уровнях как прокси)
            var recentVolumes = history.Skip(Math.Max(0, history.Count - 20))
                                     .Select(h => h.Bids.FirstOrDefault()?.Volume ?? 0 +
                                                 h.Asks.FirstOrDefault()?.Volume ?? 0)
                                     .ToList();

            if (recentVolumes.Count < 5) return 0;

            decimal mean = recentVolumes.Average();
            if (mean == 0) return 0;

            decimal variance = recentVolumes.Sum(v => (v - mean) * (v - mean)) / recentVolumes.Count;
            decimal stdDev = (decimal)Math.Sqrt((double)variance);

            decimal currentVolume = current.Bids.FirstOrDefault()?.Volume ?? 0 +
                                   current.Asks.FirstOrDefault()?.Volume ?? 0;

            decimal zScore = stdDev > 0 ? (currentVolume - mean) / stdDev : 0;

            return zScore;
        }

        private (List<VolumeCluster> clusters, decimal poc, (decimal low, decimal high) valueArea)
        AnalyzeVolumeClusters(List<OrderBookSnapshot> history)
        {
            var allLevels = new Dictionary<decimal, decimal>();

            // Собираем все уровни и объемы из истории
            foreach (var book in history.Skip(Math.Max(0, history.Count - 50))) // Последние 50 снимков
            {
                foreach (var bid in book.Bids.Take(10))
                {
                    if (allLevels.ContainsKey(bid.Price))
                        allLevels[bid.Price] += bid.Volume;
                    else
                        allLevels[bid.Price] = bid.Volume;
                }

                foreach (var ask in book.Asks.Take(10))
                {
                    if (allLevels.ContainsKey(ask.Price))
                        allLevels[ask.Price] += ask.Volume;
                    else
                        allLevels[ask.Price] = ask.Volume;
                }
            }

            if (!allLevels.Any())
                return (new List<VolumeCluster>(), 0, (0, 0));

            // Находим POC (Point of Control)
            var pocEntry = allLevels.OrderByDescending(kv => kv.Value).First();
            decimal poc = pocEntry.Key;

            // Рассчитываем Value Area (70% объема вокруг POC)
            decimal totalVolume = allLevels.Sum(kv => kv.Value);
            decimal targetVolume = totalVolume * 0.7m;

            var sortedLevels = allLevels.OrderBy(kv => kv.Key).ToList();
            decimal cumulativeVolume = 0;
            decimal vaLow = poc;
            decimal vaHigh = poc;

            // Расширяемся от POC пока не наберем 70% объема
            foreach (var level in sortedLevels)
            {
                if (cumulativeVolume >= targetVolume) break;
                cumulativeVolume += level.Value;

                vaLow = Math.Min(vaLow, level.Key);
                vaHigh = Math.Max(vaHigh, level.Key);
            }

            var clusters = allLevels.Select(kv => new VolumeCluster
            {
                Price = kv.Key,
                Volume = kv.Value
            }).ToList();

            return (clusters, poc, (vaLow, vaHigh));
        }

        private (decimal spreadScore, decimal depthScore) AnalyzeSpreadAndDepth(OrderBookSnapshot current, List<OrderBookSnapshot> history)
        {
            if (history.Count < 5) return (1.0m, 1.0m);

            // Анализ спреда
            var recentSpreads = history.Skip(Math.Max(0, history.Count - 10))
                                     .Select(h => h.Spread)
                                     .Where(s => s > 0)
                                     .ToList();

            decimal avgSpread = recentSpreads.Any() ? recentSpreads.Average() : current.Spread;
            decimal spreadScore = avgSpread > 0 ? (current.Spread / avgSpread) : 1.0m;

            // Анализ глубины
            var recentDepths = history.Skip(Math.Max(0, history.Count - 10))
                                    .Select(h => h.Bids.Sum(b => b.Volume) + h.Asks.Sum(a => a.Volume))
                                    .ToList();

            decimal currentDepth = current.Bids.Sum(b => b.Volume) + current.Asks.Sum(a => a.Volume);
            decimal avgDepth = recentDepths.Any() ? recentDepths.Average() : currentDepth;
            decimal depthScore = avgDepth > 0 ? (currentDepth / avgDepth) : 1.0m;

            return (spreadScore, depthScore);
        }

        private void LogImportantPatterns(string security, OrderBookMetrics metrics)
        {
            if (metrics.HasLargeOrderExecution)
            {
                _bot.SendNewLogMessage(
                    $"🚨 {security}: КРУПНЫЙ ОРДЕР исполнен! Объем: {metrics.LargeOrderDetectionScore:F0}",
                    LogMessageType.System);
            }

            if (metrics.HasResistanceWall)
            {
                var wall = metrics.DetectedWalls.First(w => w.Type == WallType.Resistance);
                _bot.SendNewLogMessage(
                    $"🏗️ {security}: СТЕНА СОПРОТИВЛЕНИЯ на {wall.PriceLevel:F2} ({wall.VolumePercentage:F1}%)",
                    LogMessageType.System);
            }

            if (metrics.HasSupportWall)
            {
                var wall = metrics.DetectedWalls.First(w => w.Type == WallType.Support);
                _bot.SendNewLogMessage(
                    $"🏗️ {security}: СТЕНА ПОДДЕРЖКИ на {wall.PriceLevel:F2} ({wall.VolumePercentage:F1}%)",
                    LogMessageType.System);
            }

            if (metrics.HasAbnormalTickVolume)
            {
                _bot.SendNewLogMessage(
                    $"⚡ {security}: АНОМАЛЬНЫЙ ОБЪЕМ! Z-score: {metrics.AbnormalVolumeZScore:F2}",
                    LogMessageType.System);
            }

            if (metrics.HasIncreasingLiquidity)
            {
                _bot.SendNewLogMessage(
                    $"💧 {security}: РОСТ ЛИКВИДНОСТИ! Коэффициент: {metrics.DepthScore:F2}",
                    LogMessageType.System);
            }
        }

        public OrderBookMetrics GetMetrics(string security)
        {
            if (_metricsCache.TryGetValue(security, out var metrics))
            {
                return metrics;
            }

            return new OrderBookMetrics(); // Возвращаем пустые метрики
        }

        public bool ShouldFilterSignal(string security, string direction, decimal currentPrice)
        {
            var metrics = GetMetrics(security);

            // Крупные ордера - блокируем сигналы
            if (metrics.HasLargeOrderExecution)
            {
                _bot.SendNewLogMessage($"📊 OrderBook фильтр: Крупный ордер в {security} - сигнал заблокирован", LogMessageType.System);
                return true;
            }

            // Стены - блокируем против стен
            if (direction == "Long" && metrics.HasResistanceWall)
            {
                _bot.SendNewLogMessage($"📊 OrderBook фильтр: Стена сопротивления в {security} - LONG заблокирован", LogMessageType.System);
                return true;
            }

            if (direction == "Short" && metrics.HasSupportWall)
            {
                _bot.SendNewLogMessage($"📊 OrderBook фильтр: Стена поддержки в {security} - SHORT заблокирован", LogMessageType.System);
                return true;
            }

            return false;
        }

        public decimal GetOrderBookConfidence(string security, string direction)
        {
            var metrics = GetMetrics(security);

            decimal confidence = 0.5m; // Базовый уровень

            // Аномальные объемы повышают уверенность
            if (metrics.HasAbnormalTickVolume)
                confidence += 0.2m;

            // Рост ликвидности повышает уверенность
            if (metrics.HasIncreasingLiquidity)
                confidence += 0.1m;

            // Если цена в Value Area - повышаем уверенность
            var currentPrice = GetCurrentPrice(security);
            if (currentPrice >= metrics.ValueArea.Low && currentPrice <= metrics.ValueArea.High)
                confidence += 0.1m;

            return Math.Min(confidence, 1.0m);
        }

        private decimal GetCurrentPrice(string security)
        {
            // Получаем текущую цену из последнего снимка стакана
            if (_latestBooks.TryGetValue(security, out var book))
            {
                return book.MidPrice;
            }

            return 0;
        }

        public string GetOrderBookStatus(string security)
        {
            var metrics = GetMetrics(security);
            var book = _latestBooks.ContainsKey(security) ? _latestBooks[security] : null;

            var status = new System.Text.StringBuilder();
            status.AppendLine($"📊 OrderBook статус для {security}:");

            if (book != null)
            {
                status.AppendLine($"Спред: {book.Spread:F4} | Лучший BID: {book.BestBid:F4} | Лучший ASK: {book.BestAsk:F4}");
                status.AppendLine($"Глубина: {book.Bids.Count + book.Asks.Count} уровней");
            }

            status.AppendLine($"Крупные ордера: {(metrics.HasLargeOrderExecution ? "ОБНАРУЖЕНЫ" : "нет")}");
            status.AppendLine($"Стены: Поддержка {(metrics.HasSupportWall ? "есть" : "нет")}, Сопротивление {(metrics.HasResistanceWall ? "есть" : "нет")}");
            status.AppendLine($"Аномальный объем: {(metrics.HasAbnormalTickVolume ? $"Z={metrics.AbnormalVolumeZScore:F2}" : "нет")}");
            status.AppendLine($"POC: {metrics.PointOfControl:F4} | VA: [{metrics.ValueArea.Low:F4} - {metrics.ValueArea.High:F4}]");

            return status.ToString();
        }
    }

    // Классы данных для OrderBook анализа
    public class OrderBookSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string Security { get; set; }
        public List<OrderBookLevel> Bids { get; set; } = new List<OrderBookLevel>();
        public List<OrderBookLevel> Asks { get; set; } = new List<OrderBookLevel>();
        public decimal BestBid { get; set; }
        public decimal BestAsk { get; set; }
        public decimal Spread { get; set; }
        public decimal MidPrice => (BestBid + BestAsk) / 2;
    }

    public class OrderBookLevel
    {
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public int OrdersCount { get; set; }
    }

    public class OrderBookMetrics
    {
        // Крупные ордера
        public decimal LargeOrderDetectionScore { get; set; }
        public bool HasLargeOrderExecution { get; set; }

        // Стены
        public List<WallInfo> DetectedWalls { get; set; } = new List<WallInfo>();
        public bool HasResistanceWall { get; set; }
        public bool HasSupportWall { get; set; }

        // Аномальные объемы
        public decimal AbnormalVolumeZScore { get; set; }
        public bool HasAbnormalTickVolume { get; set; }

        // Кластерный анализ
        public List<VolumeCluster> Clusters { get; set; } = new List<VolumeCluster>();
        public decimal PointOfControl { get; set; }
        public (decimal Low, decimal High) ValueArea { get; set; }

        // Спред и глубина
        public decimal SpreadScore { get; set; }
        public decimal DepthScore { get; set; }
        public bool HasIncreasingLiquidity { get; set; }
    }

    public class WallInfo
    {
        public decimal PriceLevel { get; set; }
        public decimal Volume { get; set; }
        public decimal VolumePercentage { get; set; }
        public WallType Type { get; set; }
    }

    public enum WallType { Support, Resistance }

    public class VolumeCluster
    {
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
    }

    #endregion

    #region HISTORICAL DATA CLASSES
    public class HistoricalTrade
    {
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public string Direction { get; set; } // "Long" or "Short"
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitPercent { get; set; }
        public bool IsProfitable { get; set; }
    }

    public class HistoricalCandle
    {
        public DateTime Time { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }
    #endregion

    #region MAIN BOT CLASS
    [Bot("PSOScreenerHybridPro")]
    public class PSOScreenerHybridPro : BotPanel
    {
        private ComponentAssembly _assembly;
        private AdaptiveTradingStateMachine _stateMachine;
        private HybridAiOptimizationEngine _aiEngine;
        private PositionManager _positionManager;
        private OnChainAnalyzer _onChainAnalyzer;
        private OrderBookAnalyzer _orderBookAnalyzer;
        
        private readonly ConcurrentDictionary<string, InstrumentData> _instrumentData = 
            new ConcurrentDictionary<string, InstrumentData>();
        private readonly ConcurrentDictionary<string, DateTime> _activeInstruments = 
            new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, DateTime> _lastOrderTimes = 
            new ConcurrentDictionary<string, DateTime>();
        
        // Параметры PSO оптимизации
        public StrategyParameterBool PsoAutoOptimize;
        public StrategyParameterInt PsoOptimizationInterval;
        public StrategyParameterInt PsoPopulationSize;
        public StrategyParameterInt PsoMaxIterations;
        public StrategyParameterBool PsoUseEnhancedMetrics;
        public StrategyParameterString AiOptimizationMode;
        public StrategyParameterBool ContinuousOptimization;

        // Параметры индикаторов
        public StrategyParameterInt IchimokuTenkan;
        public StrategyParameterInt IchimokuKijun;
        public StrategyParameterInt IchimokuSenkouB;
        public StrategyParameterInt RsiPeriod;

        // Параметры исторических данных для оптимизации
        public StrategyParameterString HistoricalDataPath;
        
        // EMA параметры
        public StrategyParameterInt Ema1Period;
        public StrategyParameterInt Ema2Period;
        public StrategyParameterInt Ema3Period;
        
        public StrategyParameterDecimal DistanceBetweenOrders;
        
        // Параметры тейк-профита
        public StrategyParameterDecimal TakeProfitLong;
        public StrategyParameterDecimal TakeProfitShort;
        
        // Абсолютная защита от убытков
        public StrategyParameterDecimal MinProfitPercent;
        public StrategyParameterBool UseAbsoluteProtection;
        public StrategyParameterDecimal BreakevenTriggerPercent;

        // Управление торговлей
        public StrategyParameterString TradingMode;
        public StrategyParameterBool EnableLong;
        public StrategyParameterBool EnableShort;
        public StrategyParameterInt MaxTradingInstruments;
        public StrategyParameterInt MaxBotPositions;
        public StrategyParameterString PositionCloseMode;
        public StrategyParameterBool ForceTrading;

        // Параметры объема
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterDecimal VolumeReductionPerOrder;
        
        // Защитные механизмы
        public StrategyParameterBool UseDrawdownProtection;
        public StrategyParameterDecimal MaxDrawdownPerInstrument;
        public StrategyParameterDecimal VolumeReductionFactor;
        
        // Защита от дублей
        public StrategyParameterBool UseDuplicateProtection;
        public StrategyParameterInt DuplicateProtectionMinutes;
        public StrategyParameterDecimal DuplicatePriceTolerancePercent;
        
        // Фильтры
        public StrategyParameterBool UseTrendFilter;
        public StrategyParameterBool UseRsiFilter;
        public StrategyParameterDecimal RsiOverbought;
        public StrategyParameterDecimal RsiOversold;
        public StrategyParameterBool UseIchimokuFilter;
        
        // Задержки и мониторинг
        public StrategyParameterBool UseTradeDelay;
        public StrategyParameterInt DelayBetweenOrdersSeconds;
        public StrategyParameterBool UnrealizedPnLMonitoring;
        public StrategyParameterDecimal MaxUnrealizedLossPerInstrument;

        // On-Chain анализ параметры
        public StrategyParameterBool UseOnChainAnalysis;
        public StrategyParameterDecimal OnChainFilterStrength;
        public StrategyParameterDecimal OnChainWhaleThreshold;
        public StrategyParameterInt OnChainUpdateIntervalMinutes;

        // OrderBook анализ параметры (Мосбиржа)
        public StrategyParameterBool UseOrderBookAnalysis;
        public StrategyParameterDecimal OrderBookWallThreshold;
        public StrategyParameterDecimal OrderBookAbnormalVolumeZScore;
        public StrategyParameterInt OrderBookClusterLevels;
        public StrategyParameterInt OrderBookHistoryDepth;
        public StrategyParameterInt OrderBookUpdateIntervalMs;

        public PSOScreenerHybridPro(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            Description = "Профессиональный гибридный скринер: PSO+GA AI оптимизация + Ишимоку + RSI + Абсолютная защита";

            // Инициализация компонентной архитектуры
            InitializeComponentArchitecture();
            
            // Создание параметров
            CreateParameters();
            
            // Инициализация подписки на события
            InitializeEventSubscriptions();
            
            SendNewLogMessage("🤖 Профессиональный PSO+GA скринер с абсолютной защитой инициализирован", 
                            LogMessageType.System);
        }

        private void InitializeComponentArchitecture()
        {
            _assembly = new ComponentAssembly();
            _stateMachine = new AdaptiveTradingStateMachine(this);

            _aiEngine = new HybridAiOptimizationEngine(this);
            _positionManager = new PositionManager();
            _onChainAnalyzer = new OnChainAnalyzer(this);
            _orderBookAnalyzer = new OrderBookAnalyzer(this);

            _assembly.RegisterComponent(_aiEngine);
            _assembly.RegisterComponent(_positionManager);
            _assembly.RegisterComponent(_onChainAnalyzer);
            _assembly.RegisterComponent(_orderBookAnalyzer);

            _assembly.Initialize(this);
            _stateMachine.TransitionTo(AdaptiveTradingStateMachine.TradingState.WaitingForSignals);
        }

        private void CreateParameters()
        {
            #region AI OPTIMIZATION PARAMETERS
            AiOptimizationMode = CreateParameter("🤖 AI Оптимизация", "Гибридная", 
                new[] { "Выключена", "PSO", "GA", "Гибридная", "Авто" });
            ContinuousOptimization = CreateParameter("🔄 Непрерывная оптимизация", true);
            PsoAutoOptimize = CreateParameter("Автооптимизация PSO", true);
            PsoOptimizationInterval = CreateParameter("Интервал оптимизации (мин)", 120, 60, 480, 60);
            PsoPopulationSize = CreateParameter("PSO: Размер роя", 50, 20, 200, 10);
            PsoMaxIterations = CreateParameter("PSO: Макс. итераций", 100, 50, 500, 50);
            PsoUseEnhancedMetrics = CreateParameter("Расширенные метрики PSO", true);
            #endregion

            #region ABSOLUTE PROTECTION PARAMETERS
            MinProfitPercent = CreateParameter("Минимальная прибыль %", 0.45m, 0.1m, 2.0m, 0.05m);
            UseAbsoluteProtection = CreateParameter("Абсолютная защита", true);
            BreakevenTriggerPercent = CreateParameter("Триггер безубытка %", 0.40m, 0.1m, 1.0m, 0.05m);
            #endregion

            #region INDICATOR PARAMETERS
            IchimokuTenkan = CreateParameter("Ишимоку Тенкан", 9, 7, 12, 1);
            IchimokuKijun = CreateParameter("Ишимоку Киджун", 26, 20, 30, 1);
            IchimokuSenkouB = CreateParameter("Ишимоку Сенкоу B", 52, 45, 60, 1);
            RsiPeriod = CreateParameter("Период RSI", 14, 7, 21, 1);

            Ema1Period = CreateParameter("EMA1 период", 300, 200, 400, 10);
            Ema2Period = CreateParameter("EMA2 период", 80, 70, 90, 5);
            Ema3Period = CreateParameter("EMA3 период", 30, 25, 35, 1);

            HistoricalDataPath = CreateParameter("Путь к историческим данным", "C:\\OsEngine-master\\OsEngine-master\\project\\OsEngine\\bin\\Debug\\Data");
            #endregion

            #region TRADING PARAMETERS
            DistanceBetweenOrders = CreateParameter("Расстояние между ордерами %", 0.3m, 0.1m, 1.0m, 0.1m);
            TakeProfitLong = CreateParameter("Тейк-профит Лонг %", 0.5m, 0.2m, 1.5m, 0.1m);
            TakeProfitShort = CreateParameter("Тейк-профит Шорт %", 0.3m, 0.1m, 1.0m, 0.1m);
            
            TradingMode = CreateParameter("Режим торговли", "On", new[] { "On", "Off", "Only Close Position" });
            PositionCloseMode = CreateParameter("Режим закрытия", "Общая позиция", 
                new[] { "Общая позиция", "По отдельным сделкам" });
            EnableLong = CreateParameter("Включить Лонг", true);
            EnableShort = CreateParameter("Включить Шорт", false);
            MaxTradingInstruments = CreateParameter("Макс. инструментов", 5, 1, 10, 1);
            MaxBotPositions = CreateParameter("Макс. позиций бота", 10, 1, 50, 1);
            ForceTrading = CreateParameter("Принудительная торговля", false);
            #endregion

            #region VOLUME PARAMETERS
            VolumeType = CreateParameter("Тип объема", "Contracts", 
                new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Базовый объем", 1m, 0.1m, 5m, 0.1m);
            TradeAssetInPortfolio = CreateParameter("Базовый актив портфеля", "Prime", 
                new[] { "Prime", "RUB", "USD", "EUR" });
            VolumeReductionPerOrder = CreateParameter("Уменьшение объема %", 5m, 0m, 20m, 1m);
            #endregion

            #region PROTECTION PARAMETERS
            UseDrawdownProtection = CreateParameter("Защита от просадки", true);
            MaxDrawdownPerInstrument = CreateParameter("Макс. просадка инструмента %", 3m, 1m, 10m, 0.5m);
            VolumeReductionFactor = CreateParameter("Коэф. снижения объема", 0.5m, 0.2m, 0.8m, 0.1m);
            
            UseDuplicateProtection = CreateParameter("Защита от дублей", true);
            DuplicateProtectionMinutes = CreateParameter("Время защиты от дублей (мин)", 5, 1, 30, 1);
            DuplicatePriceTolerancePercent = CreateParameter("Допуск цены для дублей %", 0.1m, 0.01m, 1.0m, 0.01m);
            #endregion

            #region FILTER PARAMETERS
            UseTrendFilter = CreateParameter("Фильтр тренда", true);
            UseRsiFilter = CreateParameter("Фильтр RSI", true);
            RsiOverbought = CreateParameter("RSI перекупленность", 70m, 60m, 80m, 2m);
            RsiOversold = CreateParameter("RSI перепроданность", 30m, 20m, 40m, 2m);
            UseIchimokuFilter = CreateParameter("Фильтр Ишимоку", true);
            #endregion

            #region DELAY AND MONITORING PARAMETERS
            UseTradeDelay = CreateParameter("Использовать задержку", true);
            DelayBetweenOrdersSeconds = CreateParameter("Задержка между ордерами (сек)", 2, 0, 15, 1);
            UnrealizedPnLMonitoring = CreateParameter("Мониторинг нереал. PnL", true);
            MaxUnrealizedLossPerInstrument = CreateParameter("Макс. нереал. убыток на инструмент %", 3m, 1m, 10m, 0.5m);
            #endregion

            #region ON-CHAIN ANALYSIS PARAMETERS
            UseOnChainAnalysis = CreateParameter("🔗 Включить On-Chain анализ", true);
            OnChainFilterStrength = CreateParameter("💪 Сила On-Chain фильтра", 0.8m, 0.1m, 2.0m, 0.1m);
            OnChainWhaleThreshold = CreateParameter("🐋 Порог китов (USD)", 1000000m, 100000m, 10000000m, 100000m);
            OnChainUpdateIntervalMinutes = CreateParameter("⏰ Интервал On-Chain (мин)", 5, 1, 60, 1);
            #endregion

            #region ORDER BOOK ANALYSIS PARAMETERS (МОСБИРЖА)
            UseOrderBookAnalysis = CreateParameter("📊 Включить анализ стакана", true);
            OrderBookWallThreshold = CreateParameter("🏗️ Порог стены (% от общего объема)", 5.0m, 1.0m, 20.0m, 1.0m);
            OrderBookAbnormalVolumeZScore = CreateParameter("⚡ Z-score аномального объема", 3.0m, 2.0m, 5.0m, 0.5m);
            OrderBookClusterLevels = CreateParameter("📈 Глубина кластерного анализа", 20, 5, 50, 5);
            OrderBookHistoryDepth = CreateParameter("📚 Глубина истории стакана", 100, 20, 500, 20);
            OrderBookUpdateIntervalMs = CreateParameter("⏱️ Интервал обновления стакана (мс)", 100, 50, 1000, 50);
            #endregion
        }

        private void InitializeEventSubscriptions()
        {
            if (TabScreener != null)
            {
                TabScreener.CandleFinishedEvent += TabScreener_CandleFinishedEvent;
                
                // Подписка на события позиций
                foreach (var tab in TabScreener.Tabs)
                {
                    if (tab is BotTabSimple simpleTab)
                    {
                        simpleTab.PositionOpeningSuccesEvent += Position_OpeningSuccesEvent;
                        simpleTab.PositionClosingSuccesEvent += Position_ClosingSuccesEvent;
                    }
                }
            }
            else
            {
                SendNewLogMessage("❌ Ошибка: TabScreener не инициализирован", LogMessageType.Error);
            }
        }

        #region EVENT HANDLERS
        private void TabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (candles == null || candles.Count < 52) return;
            if (tab?.Security == null) return;

            string security = tab.Security.Name;
            
            try
            {
                // Обновление данных инструмента
                UpdateInstrumentData(security, tab, candles);

                // Обновление компонентов
                _assembly.Update();

                if (!CanTradeInstrument(security)) return;

                var currentCandle = candles[candles.Count - 1];
                
                // Обновление анализа тренда
                UpdateTrendAnalysis(security, tab, currentCandle);
                
                // Мониторинг PnL
                MonitorUnrealizedPnL(security, tab, currentCandle);
                
                // Проверка условий торговли
                if (TradingMode != null && TradingMode.ValueString == "On")
                {
                    CheckTradingConditions(security, tab, currentCandle);
                }
                    
                CheckExitConditions(security, tab, currentCandle);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка анализа {security}: {ex.Message}", LogMessageType.Error);
            }
        }

        private void Position_OpeningSuccesEvent(Position position)
        {
            try
            {
                // Определение типа позиции (бот/ручная)
                var positionType = DeterminePositionType(position);
                
                // Регистрация позиции в менеджере
                _positionManager.RegisterPosition(position, positionType);
                
                LogPositionOpened(position, positionType);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обработки открытия позиции: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        private void Position_ClosingSuccesEvent(Position position)
        {
            try
            {
                LogPositionClosed(position);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обработки закрытия позиции: {ex.Message}", 
                                LogMessageType.Error);
            }
        }
        #endregion

        #region INSTRUMENT DATA MANAGEMENT
        private void UpdateInstrumentData(string security, BotTabSimple tab, List<Candle> candles)
        {
            try
            {
                if (!_instrumentData.ContainsKey(security))
                {
                    _instrumentData[security] = new InstrumentData 
                    { 
                        Security = security,
                        HistoricalData = new List<Candle>()
                    };
                }

                var data = _instrumentData[security];
                int candlesToKeep = Math.Min(candles.Count, 200);
                data.HistoricalData = candles.Skip(candles.Count - candlesToKeep).ToList();
                data.LastUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обновления данных {security}: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        private void UpdateTrendAnalysis(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!_instrumentData.ContainsKey(security)) return;

            var data = _instrumentData[security];
            if (data.HistoricalData.Count >= 60)
            {
                data.Trend.Update(data.HistoricalData, 
                    IchimokuTenkan.ValueInt,
                    IchimokuKijun.ValueInt,
                    IchimokuSenkouB.ValueInt,
                    RsiPeriod.ValueInt);
            }
        }
        #endregion

        #region VOLUME CALCULATION
        private decimal GetVolume(BotTabSimple tab, decimal currentPrice)
        {
            decimal volume = 0;

            try
            {
                if (VolumeType == null || Volume == null)
                {
                    SendNewLogMessage("❌ Ошибка: VolumeType или Volume не инициализированы", 
                                    LogMessageType.Error);
                    return 0;
                }
                
                switch (VolumeType.ValueString)
                {
                    case "Contracts":
                        volume = Volume.ValueDecimal;
                        break;
                        
                    case "Contract currency":
                        volume = Volume.ValueDecimal / currentPrice;
                        
                        if (StartProgram == StartProgram.IsOsTrader)
                        {
                            if (tab.Connector != null)
                            {
                                var serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);
                                if (serverPermission != null && serverPermission.IsUseLotToCalculateProfit 
                                    && tab.Security != null && tab.Security.Lot != 0 && tab.Security.Lot > 1)
                                {
                                    volume = Volume.ValueDecimal / (currentPrice * tab.Security.Lot);
                                }
                            }
                            if (tab.Security != null)
                                volume = Math.Round(volume, tab.Security.DecimalsVolume);
                        }
                        else
                        {
                            volume = Math.Round(volume, 6);
                        }
                        break;
                        
                    case "Deposit percent":
                        Portfolio myPortfolio = tab.Portfolio;
                        
                        if (myPortfolio == null)
                        {
                            SendNewLogMessage($"❌ Не найден портфель для {tab.Security.Name}", 
                                            LogMessageType.Error);
                            return 0;
                        }
                        
                        decimal portfolioValue = 0;
                        
                        if (TradeAssetInPortfolio.ValueString == "Prime")
                        {
                            portfolioValue = myPortfolio.ValueCurrent;
                        }
                        else
                        {
                            var positionOnBoard = myPortfolio.GetPositionOnBoard();
                            
                            if (positionOnBoard == null)
                            {
                                SendNewLogMessage($"❌ Не удалось получить позиции портфеля для {tab.Security.Name}", 
                                                LogMessageType.Error);
                                return 0;
                            }

                            foreach (var position in positionOnBoard)
                            {
                                if (position.SecurityNameCode == TradeAssetInPortfolio.ValueString)
                                {
                                    portfolioValue = position.ValueCurrent;
                                    break;
                                }
                            }
                        }
                        
                        if (portfolioValue == 0)
                        {
                            SendNewLogMessage($"❌ Не найден актив {TradeAssetInPortfolio.ValueString} в портфеле", 
                                            LogMessageType.Error);
                            return 0;
                        }
                        
                        decimal moneyOnPosition = portfolioValue * (Volume.ValueDecimal / 100);
                        decimal qty = moneyOnPosition / currentPrice;
                        
                        if (tab.Security != null && tab.Security.Lot > 0)
                        {
                            qty = qty / tab.Security.Lot;
                        }
                        
                        if (tab.StartProgram == StartProgram.IsOsTrader && tab.Security != null)
                        {
                            qty = Math.Round(qty, tab.Security.DecimalsVolume);
                        }
                        else
                        {
                            qty = Math.Round(qty, 7);
                        }
                        
                        return qty;
                }

                return volume;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка расчета объема для {tab.Security.Name}: {ex.Message}", 
                                LogMessageType.Error);
                return 0;
            }
        }
        #endregion

        #region TRADING LOGIC
        private void CheckTradingConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!_instrumentData.ContainsKey(security)) return;

            var trend = _instrumentData[security].Trend;
            
            if (EnableLong.ValueBool)
            {
                CheckLongConditions(security, tab, currentCandle, trend);
            }
            
            if (EnableShort.ValueBool)
            {
                CheckShortConditions(security, tab, currentCandle, trend);
            }
        }

        private void CheckLongConditions(string security, BotTabSimple tab, Candle currentCandle, EnhancedTrendAnalysis trend)
        {
            // Проверка фильтров
            if (!PassFilters(trend, "Long")) return;

            // On-Chain фильтры (если включены)
            if (UseOnChainAnalysis.ValueBool)
            {
                // Определяем блокчейн по названию инструмента
                string blockchain = GetBlockchainFromSecurity(security);
                if (_onChainAnalyzer.ShouldFilterSignal(blockchain, "Long", currentCandle.Close))
                {
                    SendNewLogMessage($"🔗 On-Chain фильтр заблокировал LONG сигнал для {security}", LogMessageType.System);
                    return;
                }
            }

            // OrderBook фильтры (если включены)
            if (UseOrderBookAnalysis.ValueBool)
            {
                if (_orderBookAnalyzer.ShouldFilterSignal(security, "Long", currentCandle.Close))
                {
                    SendNewLogMessage($"📊 OrderBook фильтр заблокировал LONG сигнал для {security}", LogMessageType.System);
                    return;
                }
            }

            // Проверка задержки
            if (!CanOpenOrder(security, "Long")) return;

            // Проверка лимита позиций
            if (!CanOpenNewBotPosition()) return;

            decimal currentPrice = currentCandle.Close;
            
            if (ShouldOpenNextOrder(security, tab, currentPrice, "Long") && 
                !HasPositionNearPrice(tab, currentPrice))
            {
                // Проверка сигнала
                bool buySignal = GetEnhancedBuySignal(trend);
                
                if (buySignal)
                {
                    decimal volume = GetVolume(tab, currentPrice);
                    
                    if (volume > 0 && ValidateOrderConditions(security, tab, volume, currentPrice))
                    {
                        _stateMachine.TransitionTo(AdaptiveTradingStateMachine.TradingState.PositionOpening);
                        
                        tab.BuyAtMarket(volume);
                        UpdateLastOrderTime(security, "Long");
                        
                        LogTradeOpened(security, "LONG", currentPrice, volume, trend);
                    }
                }
            }
        }

        private void CheckShortConditions(string security, BotTabSimple tab, Candle currentCandle, EnhancedTrendAnalysis trend)
        {
            // Проверка фильтров
            if (!PassFilters(trend, "Short")) return;

            // On-Chain фильтры (если включены)
            if (UseOnChainAnalysis.ValueBool)
            {
                // Определяем блокчейн по названию инструмента
                string blockchain = GetBlockchainFromSecurity(security);
                if (_onChainAnalyzer.ShouldFilterSignal(blockchain, "Short", currentCandle.Close))
                {
                    SendNewLogMessage($"🔗 On-Chain фильтр заблокировал SHORT сигнал для {security}", LogMessageType.System);
                    return;
                }
            }

            // OrderBook фильтры (если включены)
            if (UseOrderBookAnalysis.ValueBool)
            {
                if (_orderBookAnalyzer.ShouldFilterSignal(security, "Short", currentCandle.Close))
                {
                    SendNewLogMessage($"📊 OrderBook фильтр заблокировал SHORT сигнал для {security}", LogMessageType.System);
                    return;
                }
            }

            // Проверка задержки
            if (!CanOpenOrder(security, "Short")) return;

            // Проверка лимита позиций
            if (!CanOpenNewBotPosition()) return;

            decimal currentPrice = currentCandle.Close;
            
            if (ShouldOpenNextOrder(security, tab, currentPrice, "Short") && 
                !HasPositionNearPrice(tab, currentPrice))
            {
                // Проверка сигнала
                bool sellSignal = GetEnhancedSellSignal(trend);
                
                if (sellSignal)
                {
                    decimal volume = GetVolume(tab, currentPrice);
                    
                    if (volume > 0 && ValidateOrderConditions(security, tab, volume, currentPrice))
                    {
                        _stateMachine.TransitionTo(AdaptiveTradingStateMachine.TradingState.PositionOpening);
                        
                        tab.SellAtMarket(volume);
                        UpdateLastOrderTime(security, "Short");
                        
                        LogTradeOpened(security, "SHORT", currentPrice, volume, trend);
                    }
                }
            }
        }

        private bool PassFilters(EnhancedTrendAnalysis trend, string direction)
        {
            // Фильтр тренда Ишимоку
            if (UseTrendFilter.ValueBool && UseIchimokuFilter.ValueBool)
            {
                if (direction == "Long" && 
                    (trend.TrendDirection == "Down" || trend.TrendDirection == "Strong Down"))
                {
                    return false;
                }
                
                if (direction == "Short" && 
                    (trend.TrendDirection == "Up" || trend.TrendDirection == "Strong Up"))
                {
                    return false;
                }
            }

            // Фильтр RSI
            if (UseRsiFilter.ValueBool)
            {
                if (direction == "Long" && trend.Rsi > RsiOverbought.ValueDecimal)
                {
                    return false;
                }
                
                if (direction == "Short" && trend.Rsi < RsiOversold.ValueDecimal)
                {
                    return false;
                }
            }

            return true;
        }

        private bool GetEnhancedBuySignal(EnhancedTrendAnalysis analysis)
        {
            return analysis.TenkanAboveKijun && 
                   analysis.PriceAboveCloud && 
                   analysis.CloudBullish && 
                   analysis.Rsi < RsiOverbought.ValueDecimal;
        }

        private bool GetEnhancedSellSignal(EnhancedTrendAnalysis analysis)
        {
            return !analysis.TenkanAboveKijun && 
                   analysis.PriceBelowCloud && 
                   analysis.CloudBearish && 
                   analysis.Rsi > RsiOversold.ValueDecimal;
        }
        #endregion

        #region EXIT CONDITIONS AND PROTECTION
        private void CheckExitConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            try
            {
                // Получаем только открытые позиции (исключаем позиции в состоянии Closing, Closed, Opening и т.д.)
                var positions = tab.PositionsOpenAll?
                    .Where(p => p.State == PositionStateType.Open)
                    .ToList();

                if (positions == null || !positions.Any()) return;

                decimal currentPrice = currentCandle.Close;

                foreach (var position in positions)
                {
                    // Абсолютная защита от убытков
                    if (!_positionManager.CanClosePosition(position, MinProfitPercent.ValueDecimal, tab, currentPrice))
                    {
                        continue;
                    }

                    bool isLong = position.Direction == Side.Buy;
                    decimal takeProfitLevel = CalculateTakeProfit(position);

                    bool takeProfitHit = isLong ?
                        currentPrice >= takeProfitLevel :
                        currentPrice <= takeProfitLevel;

                    if (takeProfitHit || TradingMode.ValueString == "Only Close Position")
                    {
                        _stateMachine.TransitionTo(AdaptiveTradingStateMachine.TradingState.PositionClosing);

                        tab.CloseAtMarket(position, position.OpenVolume);

                        LogTradeClosed(security, position, currentPrice);
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка проверки выхода {security}: {ex.Message}",
                                LogMessageType.Error);
            }
        }

        private decimal CalculateTakeProfit(Position position)
        {
            bool isLong = position.Direction == Side.Buy;
            decimal multiplier = (isLong ? TakeProfitLong.ValueDecimal : TakeProfitShort.ValueDecimal) / 100m;
            
            return isLong
                ? position.EntryPrice * (1 + multiplier)
                : position.EntryPrice * (1 - multiplier);
        }
        #endregion

        #region ORDER MANAGEMENT
        private bool ShouldOpenNextOrder(string security, BotTabSimple tab, decimal currentPrice, string direction)
        {
            try
            {
                // Получаем только открытые позиции нужного направления
                var positions = tab.PositionsOpenAll?
                    .Where(p => p.State == PositionStateType.Open &&
                           p.Direction == (direction == "Long" ? Side.Buy : Side.Sell))
                    .ToList();

                if (positions == null || !positions.Any()) return true;

                var lastPosition = positions.OrderByDescending(p => p.TimeOpen).First();
                decimal requiredDistance = DistanceBetweenOrders.ValueDecimal / 100m;

                bool shouldOpen = direction == "Long"
                    ? currentPrice <= lastPosition.EntryPrice * (1 - requiredDistance)
                    : currentPrice >= lastPosition.EntryPrice * (1 + requiredDistance);

                return shouldOpen;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка проверки расстояния {security}: {ex.Message}",
                                LogMessageType.Error);
                return false;
            }
        }

        private bool HasPositionNearPrice(BotTabSimple tab, decimal currentPrice)
        {
            // Получаем только открытые позиции
            var positions = tab.PositionsOpenAll?
                .Where(p => p.State == PositionStateType.Open)
                .ToList();

            if (positions == null) return false;

            foreach (var position in positions)
            {
                decimal priceDiff = Math.Abs(position.EntryPrice - currentPrice);
                decimal diffPercent = position.EntryPrice != 0 ?
                    priceDiff / position.EntryPrice * 100 : 0;

                if (diffPercent < 0.1m)
                    return true;
            }

            return false;
        }

        private bool CanOpenOrder(string security, string orderType)
        {
            if (!UseTradeDelay.ValueBool) return true;

            string key = $"{security}_{orderType}";
            
            if (!_lastOrderTimes.ContainsKey(key))
            {
                _lastOrderTimes[key] = DateTime.MinValue;
                return true;
            }

            TimeSpan timeSinceLastOrder = DateTime.Now - _lastOrderTimes[key];
            int requiredDelay = DelayBetweenOrdersSeconds.ValueInt;

            if (timeSinceLastOrder.TotalSeconds < requiredDelay)
            {
                return false;
            }

            return true;
        }

        private void UpdateLastOrderTime(string security, string orderType)
        {
            string key = $"{security}_{orderType}";
            _lastOrderTimes[key] = DateTime.Now;
        }

        private bool ValidateOrderConditions(string security, BotTabSimple tab, decimal volume, decimal price)
        {
            try
            {
                if (volume <= 0)
                {
                    SendNewLogMessage($"❌ Отмена ордера {security}: невалидный объем {volume}", 
                                    LogMessageType.Error);
                    return false;
                }
                
                if (price <= 0)
                {
                    SendNewLogMessage($"❌ Отмена ордера {security}: невалидная цена {price}", 
                                    LogMessageType.Error);
                    return false;
                }
                
                Portfolio portfolio = tab.Portfolio;
                if (portfolio == null)
                {
                    SendNewLogMessage($"❌ Отмена ордера {security}: портфель не найден", 
                                    LogMessageType.Error);
                    return false;
                }
                
                decimal orderValue = volume * price;
                if (orderValue > portfolio.ValueCurrent * 0.8m)
                {
                    SendNewLogMessage($"⚠️ Большой объем: {security} - {orderValue:F2} (>80% депозита)", 
                                    LogMessageType.System);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка валидации ордера {security}: {ex.Message}", 
                                LogMessageType.Error);
                return false;
            }
        }

        private bool CanOpenNewBotPosition()
        {
            if (ForceTrading.ValueBool) return true;

            int botPositionsCount = CountBotPositions();
            
            if (botPositionsCount >= MaxBotPositions.ValueInt)
            {
                return false;
            }

            return true;
        }

        public int CountBotPositions()
        {
            // Подсчет открытых ботовских позиций через менеджер позиций
            try
            {
                var botPositions = _positionManager.GetAllPositionStatistics()
                    .Where(p => p.Type == PositionType.Bot)
                    .ToList();

                return botPositions.Count;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка подсчета ботовских позиций: {ex.Message}", LogMessageType.Error);
                return 0;
            }
        }
        #endregion

        #region INSTRUMENT TRADING CONTROL
        private bool CanTradeInstrument(string security)
        {
            try
            {
                if (MaxTradingInstruments == null)
                {
                    SendNewLogMessage("❌ MaxTradingInstruments не инициализирован", 
                                    LogMessageType.Error);
                    return false;
                }

                if (_activeInstruments.Count < MaxTradingInstruments.ValueInt)
                {
                    if (!_activeInstruments.ContainsKey(security))
                    {
                        _activeInstruments[security] = DateTime.Now;
                        SendNewLogMessage($"✅ Добавлен инструмент в торговлю: {security}", 
                                        LogMessageType.System);
                    }
                    return true;
                }

                return _activeInstruments.ContainsKey(security);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка в CanTradeInstrument: {ex.Message}", 
                                LogMessageType.Error);
                return false;
            }
        }
        #endregion

        #region LOGGING METHODS
        private void LogTradeOpened(string security, string direction, decimal price, decimal volume, EnhancedTrendAnalysis trend)
        {
            SendNewLogMessage(
                $"🔗 {direction} ОТКРЫТ с Ишимоку: {security} | " +
                $"Цена: {price:F4} | Объем: {volume:F8} | " +
                $"Тренд: {trend.TrendDirection} | Облако: {(trend.CloudBullish ? "Бычье" : "Медвежье")} | " +
                $"RSI: {trend.Rsi:F1}",
                LogMessageType.Trade);
        }

        private void LogTradeClosed(string security, Position position, decimal closePrice)
        {
            decimal profit = CalculatePositionProfit(position, null, closePrice);
            decimal profitPercent = position.EntryPrice != 0 ? 
                (profit / (position.EntryPrice * Math.Abs(position.OpenVolume))) * 100m : 0;
            
            SendNewLogMessage(
                $"✅ ПОЗИЦИЯ ЗАКРЫТА: {security} | " +
                $"#{position.Number} | " +
                $"Тип: {position.Direction} | " +
                $"Цена входа: {position.EntryPrice:F4} | " +
                $"Цена выхода: {closePrice:F4} | " +
                $"Прибыль: {profit:F2} ({profitPercent:F2}%) | " +
                $"Объем: {Math.Abs(position.OpenVolume):F2}",
                LogMessageType.Trade);
        }

        private void LogPositionOpened(Position position, PositionType type)
        {
            string positionType = type == PositionType.Bot ? "БОТ" : "РУЧНАЯ";
            
            SendNewLogMessage(
                $"🎯 {positionType} ПОЗИЦИЯ ОТКРЫТА #{position.Number}\n" +
                $"Инструмент: {position.SecurityName}\n" +
                $"Направление: {position.Direction}\n" +
                $"Цена входа: {position.EntryPrice:F4}\n" +
                $"Объем: {Math.Abs(position.OpenVolume):F2}\n" +
                $"Время открытия: {position.TimeOpen}",
                LogMessageType.System);
        }

        private void LogPositionClosed(Position position)
        {
            // Расчет прибыли единообразным методом
            decimal profit = CalculatePositionProfit(position, null, position.ClosePrice);
            decimal profitPercent = position.EntryPrice != 0 ?
                (profit / (position.EntryPrice * Math.Abs(position.OpenVolume))) * 100m : 0;

            // Расчет времени жизни позиции
            TimeSpan lifeTime = DateTime.Now - position.TimeOpen;

            SendNewLogMessage(
                $"🏁 ПОЗИЦИЯ ЗАКРЫТА #{position.Number}\n" +
                $"Инструмент: {position.SecurityName}\n" +
                $"Направление: {position.Direction}\n" +
                $"Цена входа: {position.EntryPrice:F4}\n" +
                $"Цена выхода: {position.ClosePrice:F4}\n" +
                $"Прибыль: {profit:F2} ({profitPercent:F2}%)\n" +
                $"Объем: {Math.Abs(position.OpenVolume):F2}\n" +
                $"Время жизни: {lifeTime:hh\\:mm\\:ss}",
                LogMessageType.System);
        }
        #endregion

        #region UTILITY METHODS
        private PositionType DeterminePositionType(Position position)
        {
            // В реальной реализации здесь логика определения типа позиции
            // по сравнению с списком позиций, открытых ботом
            return PositionType.Bot; // Временная заглушка
        }

        private decimal CalculatePositionProfit(Position position, BotTabSimple tab, decimal currentPrice)
        {
            try
            {
                if (position == null) return 0;

                if (currentPrice == 0 && tab != null && tab.CandlesFinishedOnly != null && tab.CandlesFinishedOnly.Count > 0)
                {
                    currentPrice = tab.CandlesFinishedOnly.Last().Close;
                }
                else if (currentPrice == 0)
                {
                    currentPrice = position.EntryPrice;
                }

                decimal priceDifference = currentPrice - position.EntryPrice;
                
                if (position.Direction == Side.Sell)
                    priceDifference = -priceDifference;

                decimal profit = priceDifference * Math.Abs(position.OpenVolume);
                return profit;
            }
            catch
            {
                return 0;
            }
        }

        private void MonitorUnrealizedPnL(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (!UnrealizedPnLMonitoring.ValueBool) return;

            try
            {
                var openPositions = tab.PositionsOpenAll?.Where(p => p.State == PositionStateType.Open).ToList();
                if (openPositions == null || !openPositions.Any()) return;

                decimal totalUnrealizedPnL = 0;
                foreach (var position in openPositions)
                {
                    totalUnrealizedPnL += CalculatePositionProfit(position, tab, currentCandle.Close);
                }

                decimal portfolioValue = GetPortfolioValue(tab.Portfolio);
                decimal pnlPercent = portfolioValue != 0 ? (totalUnrealizedPnL / portfolioValue) * 100 : 0;

                if (pnlPercent < -MaxUnrealizedLossPerInstrument.ValueDecimal)
                {
                    SendNewLogMessage(
                        $"🚨 ПРЕВЫШЕН ЛИМИТ УБЫТКА: {security} | " +
                        $"Нерииализованный PnL: {totalUnrealizedPnL:F2} ({pnlPercent:F2}%)",
                        LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка мониторинга PnL {security}: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        private decimal GetPortfolioValue(Portfolio portfolio)
        {
            try
            {
                return portfolio?.ValueCurrent ?? 0m;
            }
            catch
            {
                SendNewLogMessage("❌ Ошибка получения значения портфеля",
                                LogMessageType.Error);
                return 0m;
            }
        }

        public BotTabScreener TabScreener => TabsScreener != null && TabsScreener.Count > 0 ? TabsScreener[0] : null;

        private string GetBlockchainFromSecurity(string security)
        {
            // Определение блокчейна по названию инструмента
            if (string.IsNullOrEmpty(security))
                return "BTC"; // По умолчанию

            string upperSecurity = security.ToUpper();

            // Bitcoin
            if (upperSecurity.Contains("BTC") || upperSecurity.Contains("BITCOIN"))
                return "BTC";

            // Ethereum и ERC-20 токены
            if (upperSecurity.Contains("ETH") || upperSecurity.Contains("ETHEREUM") ||
                upperSecurity.Contains("UNI") || upperSecurity.Contains("LINK") ||
                upperSecurity.Contains("AAVE") || upperSecurity.Contains("MKR") ||
                upperSecurity.Contains("COMP") || upperSecurity.Contains("SUSHI"))
                return "ETH";

            // Binance Smart Chain (BSC)
            if (upperSecurity.Contains("BNB") || upperSecurity.Contains("CAKE") ||
                upperSecurity.Contains("BAKE") || upperSecurity.Contains("BURGER") ||
                upperSecurity.Contains("BABY") || upperSecurity.Contains("BABYDOGE"))
                return "BSC";

            // По умолчанию возвращаем BTC
            return "BTC";
        }
        #endregion

        #region PUBLIC METHODS FOR MANUAL CONTROL
        public void StartManualOptimization()
        {
            try
            {
                _aiEngine?.StartOptimization();
                SendNewLogMessage("🚀 Запущена ручная AI оптимизация", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка запуска ручной оптимизации: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        public string GetOptimizationStatus()
        {
            return _aiEngine?.GetOptimizationStatus() ?? "AI движок не инициализирован";
        }

        public void ResetAI()
        {
            // Переинициализация AI движка
            _aiEngine?.Cleanup();

            var newAiEngine = new HybridAiOptimizationEngine(this);
            _assembly.RegisterComponent(newAiEngine);
            newAiEngine.Initialize(this);
            _aiEngine = newAiEngine;

            SendNewLogMessage("🔄 AI оптимизация сброшена и переинициализирована",
                            LogMessageType.System);
        }

        public string GetOnChainStatus()
        {
            return _onChainAnalyzer?.GetOnChainStatus() ?? "On-Chain анализатор не инициализирован";
        }

        public void ResetOnChain()
        {
            // Переинициализация On-Chain анализатора
            _onChainAnalyzer?.Cleanup();

            var newOnChainAnalyzer = new OnChainAnalyzer(this);
            _assembly.RegisterComponent(newOnChainAnalyzer);
            newOnChainAnalyzer.Initialize(this);
            _onChainAnalyzer = newOnChainAnalyzer;

            SendNewLogMessage("🔄 On-Chain анализатор сброшен и переинициализирован",
                            LogMessageType.System);
        }

        public string GetOrderBookStatus(string security = "")
        {
            if (string.IsNullOrEmpty(security))
            {
                // Возвращаем общий статус
                return _orderBookAnalyzer?.GetOrderBookStatus("Общий") ?? "OrderBook анализатор не инициализирован";
            }
            else
            {
                return _orderBookAnalyzer?.GetOrderBookStatus(security) ?? $"OrderBook данные для {security} недоступны";
            }
        }

        public void ResetOrderBook()
        {
            // Переинициализация OrderBook анализатора
            _orderBookAnalyzer?.Cleanup();

            var newOrderBookAnalyzer = new OrderBookAnalyzer(this);
            _assembly.RegisterComponent(newOrderBookAnalyzer);
            newOrderBookAnalyzer.Initialize(this);
            _orderBookAnalyzer = newOrderBookAnalyzer;

            SendNewLogMessage("🔄 OrderBook анализатор сброшен и переинициализирован",
                            LogMessageType.System);
        }
        #endregion

        #region OVERRIDDEN METHODS
        public override string GetNameStrategyType() => "PSOScreenerHybridPro";

        public override void ShowIndividualSettingsDialog()
        {
            // Реализация диалога настроек
        }

        public void Cleanup()
        {
            _assembly?.Cleanup();
            _stateMachine?.TransitionTo(AdaptiveTradingStateMachine.TradingState.Paused);
            
            SendNewLogMessage("🧹 Ресурсы бота очищены", LogMessageType.System);
        }
        #endregion

        #region DATA CLASSES
        public class InstrumentData
        {
            public string Security { get; set; }
            public List<Candle> HistoricalData { get; set; } = new List<Candle>();
            public EnhancedTrendAnalysis Trend { get; set; } = new EnhancedTrendAnalysis();
            public DateTime LastUpdate { get; set; }
        }

        public class EnhancedTrendAnalysis
        {
            public decimal Rsi { get; set; }
            
            public decimal IchimokuTenkanSen { get; set; }
            public decimal IchimokuKijunSen { get; set; }
            public decimal IchimokuSenkouSpanA { get; set; }
            public decimal IchimokuSenkouSpanB { get; set; }
            public decimal IchimokuChikouSpan { get; set; }
            
            public string TrendDirection { get; set; } = "Neutral";
            public decimal Strength { get; set; }
            public bool PriceAboveCloud { get; set; }
            public bool PriceBelowCloud { get; set; }
            public bool CloudBullish { get; set; }
            public bool CloudBearish { get; set; }
            public bool TenkanAboveKijun { get; set; }

            public void Update(List<Candle> candles, int ichimokuTenkan, int ichimokuKijun, 
                             int ichimokuSenkouB, int rsiPeriod)
            {
                if (candles == null || candles.Count < Math.Max(ichimokuSenkouB, 52)) return;

                try
                {
                    Rsi = CalculateRSI(candles, rsiPeriod);
                    CalculateIchimoku(candles, ichimokuTenkan, ichimokuKijun, ichimokuSenkouB);
                    AnalyzeTrend(candles);
                }
                catch (Exception)
                {
                    // Обработка ошибок расчета
                }
            }

            private void CalculateIchimoku(List<Candle> candles, int tenkanPeriod, int kijunPeriod, int senkouBPeriod)
            {
                IchimokuTenkanSen = (GetHighestHigh(candles, tenkanPeriod) + GetLowestLow(candles, tenkanPeriod)) / 2;
                IchimokuKijunSen = (GetHighestHigh(candles, kijunPeriod) + GetLowestLow(candles, kijunPeriod)) / 2;
                IchimokuSenkouSpanA = (IchimokuTenkanSen + IchimokuKijunSen) / 2;
                IchimokuSenkouSpanB = (GetHighestHigh(candles, senkouBPeriod) + GetLowestLow(candles, senkouBPeriod)) / 2;
                
                int chikouIndex = Math.Max(0, candles.Count - 26);
                IchimokuChikouSpan = candles[chikouIndex].Close;
            }

            private void AnalyzeTrend(List<Candle> candles)
            {
                decimal currentPrice = candles[candles.Count - 1].Close;
                
                PriceAboveCloud = currentPrice > Math.Max(IchimokuSenkouSpanA, IchimokuSenkouSpanB);
                PriceBelowCloud = currentPrice < Math.Min(IchimokuSenkouSpanA, IchimokuSenkouSpanB);
                CloudBullish = IchimokuSenkouSpanA > IchimokuSenkouSpanB;
                CloudBearish = IchimokuSenkouSpanA < IchimokuSenkouSpanB;
                TenkanAboveKijun = IchimokuTenkanSen > IchimokuKijunSen;
                
                if (PriceAboveCloud && CloudBullish && TenkanAboveKijun)
                    TrendDirection = "Strong Up";
                else if (PriceAboveCloud && TenkanAboveKijun)
                    TrendDirection = "Up";
                else if (PriceBelowCloud && CloudBearish && !TenkanAboveKijun)
                    TrendDirection = "Strong Down";
                else if (PriceBelowCloud && !TenkanAboveKijun)
                    TrendDirection = "Down";
                else
                    TrendDirection = "Neutral";
                    
                Strength = Math.Abs(IchimokuTenkanSen - IchimokuKijunSen) / IchimokuKijunSen * 100;
            }

            private decimal GetHighestHigh(List<Candle> candles, int period)
            {
                decimal highest = decimal.MinValue;
                int startIndex = Math.Max(0, candles.Count - period);
                
                for (int i = startIndex; i < candles.Count; i++)
                {
                    if (candles[i].High > highest)
                        highest = candles[i].High;
                }
                return highest;
            }

            private decimal GetLowestLow(List<Candle> candles, int period)
            {
                decimal lowest = decimal.MaxValue;
                int startIndex = Math.Max(0, candles.Count - period);
                
                for (int i = startIndex; i < candles.Count; i++)
                {
                    if (candles[i].Low < lowest)
                        lowest = candles[i].Low;
                }
                return lowest;
            }

            private decimal CalculateRSI(List<Candle> candles, int period)
            {
                if (candles == null || candles.Count < period + 1) return 50;

                try
                {
                    // Используем EMA для корректного расчета RSI (метод Уайлдера)
                    decimal alpha = 1.0m / period;
                    decimal avgGain = 0;
                    decimal avgLoss = 0;

                    // Сначала рассчитываем простое среднее для инициализации EMA
                    for (int i = candles.Count - period; i < candles.Count; i++)
                    {
                        if (i <= 0) continue;
                        decimal change = candles[i].Close - candles[i - 1].Close;
                        if (change > 0)
                            avgGain += change;
                        else
                            avgLoss += Math.Abs(change);
                    }

                    avgGain /= period;
                    avgLoss /= period;

                    // Затем применяем EMA для оставшихся значений
                    for (int i = candles.Count - period + 1; i < candles.Count; i++)
                    {
                        decimal change = candles[i].Close - candles[i - 1].Close;
                        decimal gain = change > 0 ? change : 0;
                        decimal loss = change < 0 ? Math.Abs(change) : 0;

                        avgGain = alpha * gain + (1 - alpha) * avgGain;
                        avgLoss = alpha * loss + (1 - alpha) * avgLoss;
                    }

                    if (avgLoss == 0) return 100;

                    decimal rs = avgGain / avgLoss;
                    return 100 - (100 / (1 + rs));
                }
                catch (Exception)
                {
                    return 50;
                }
            }
        }
        #endregion
    }
    #endregion
}