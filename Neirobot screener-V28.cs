using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Logging;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;

namespace OsEngine.Robots.PSO
{
    #region ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ
    public class InstrumentData
    {
        public string Security { get; set; }
        public List<Candle> HistoricalData { get; set; } = new List<Candle>();
        public DateTime LastUpdate { get; set; }
        public string TimeFrame { get; set; }
        public int DataQualityScore { get; set; } = 100;
        public string DataSource { get; set; }
        public DateTime FirstCandleDate { get; set; }
        public DateTime LastCandleDate { get; set; }
        public long TotalCandles { get; set; }
        public EnhancedTrendAnalysis Trend { get; set; } = new EnhancedTrendAnalysis(null);
    }

    public class EnhancedTrendAnalysis
    {
        private BotPanel _bot;

        public EnhancedTrendAnalysis(BotPanel bot = null)
        {
            _bot = bot;
        }

        public string CurrentTrend { get; set; } = "Neutral";
        public decimal TrendStrength { get; set; } = 0;
        public DateTime LastUpdate { get; set; }
        public decimal IchimokuTenkan { get; set; }
        public decimal IchimokuKijun { get; set; }
        public decimal IchimokuSenkouA { get; set; }
        public decimal IchimokuSenkouB { get; set; }
        public decimal RSI { get; set; }
        public bool IsBullishSignal { get; set; }
        public bool IsBearishSignal { get; set; }

        // Добавляем свойства для совместимости с новыми методами
        public string TrendDirection => CurrentTrend;
        public decimal Rsi => RSI;
        public bool TenkanAboveKijun => IchimokuTenkan > IchimokuKijun;
        public bool PriceAboveCloud { get; set; }
        public bool PriceBelowCloud { get; set; }
        public bool CloudBullish { get; set; }
        public bool CloudBearish { get; set; }

        public void Update(List<Candle> candles, int tenkanPeriod, int kijunPeriod, int senkouBPeriod, int rsiPeriod, decimal currentRsi = 0)
        {
            try
            {
                if (candles == null || candles.Count < Math.Max(kijunPeriod, senkouBPeriod))
                {
                    return;
                }

                LastUpdate = DateTime.Now;

                // Расчет Ichimoku значений
                var recentCandles = candles.TakeLast(Math.Max(kijunPeriod, senkouBPeriod)).ToList();

                // Tenkan-sen
                if (recentCandles.Count >= tenkanPeriod)
                {
                    var tenkanCandles = recentCandles.TakeLast(tenkanPeriod);
                    decimal high = tenkanCandles.Max(c => c.High);
                    decimal low = tenkanCandles.Min(c => c.Low);
                    IchimokuTenkan = (high + low) / 2;
                }

                // Kijun-sen
                decimal kijunHigh = recentCandles.Max(c => c.High);
                decimal kijunLow = recentCandles.Min(c => c.Low);
                IchimokuKijun = (kijunHigh + kijunLow) / 2;

                // Senkou Span A
                IchimokuSenkouA = (IchimokuTenkan + IchimokuKijun) / 2;

                // Senkou Span B
                if (recentCandles.Count >= senkouBPeriod)
                {
                    decimal senkouBHigh = recentCandles.Max(c => c.High);
                    decimal senkouBLow = recentCandles.Min(c => c.Low);
                    IchimokuSenkouB = (senkouBHigh + senkouBLow) / 2;
                }

                // Определение тренда
                if (IchimokuTenkan > IchimokuKijun && candles.Last().Close > IchimokuSenkouA)
                {
                    CurrentTrend = "Bullish";
                    TrendStrength = Math.Max(0, IchimokuTenkan - IchimokuKijun);
                }
                else if (IchimokuTenkan < IchimokuKijun && candles.Last().Close < IchimokuSenkouB)
                {
                    CurrentTrend = "Bearish";
                    TrendStrength = Math.Max(0, IchimokuKijun - IchimokuTenkan);
                }
                else
                {
                    CurrentTrend = "Neutral";
                    TrendStrength = 0;
                }

                // Используем переданный RSI или рассчитываем
                RSI = currentRsi > 0 ? currentRsi : CalculateRSI(candles, rsiPeriod);

                // Определение сигналов
                IsBullishSignal = IchimokuTenkan > IchimokuKijun && candles.Last().Close > Math.Max(IchimokuSenkouA, IchimokuSenkouB);
                IsBearishSignal = IchimokuTenkan < IchimokuKijun && candles.Last().Close < Math.Min(IchimokuSenkouA, IchimokuSenkouB);

                // Обновляем свойства облака
                CloudBullish = IchimokuSenkouA > IchimokuSenkouB;
                CloudBearish = IchimokuSenkouA < IchimokuSenkouB;
                PriceAboveCloud = candles.Last().Close > Math.Max(IchimokuSenkouA, IchimokuSenkouB);
                PriceBelowCloud = candles.Last().Close < Math.Min(IchimokuSenkouA, IchimokuSenkouB);
            }
            catch (Exception ex)
            {
                // В случае ошибки оставляем предыдущие значения
                if (_bot != null)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка обновления анализа тренда: {ex.Message}", LogMessageType.Error);
                }
            }
        }

        private decimal CalculateRSI(List<Candle> candles, int period)
        {
            try
            {
                if (candles.Count < period + 1)
                    return 50;

                var recentCandles = candles.Skip(Math.Max(0, candles.Count - (period + 1))).Take(period + 1).ToList();
                if (recentCandles.Count < period + 1)
                    return 50;

                var gains = new List<decimal>();
                var losses = new List<decimal>();

                for (int i = 1; i < recentCandles.Count; i++)
                {
                    decimal change = recentCandles[i].Close - recentCandles[i - 1].Close;
                    if (change > 0)
                        gains.Add(change);
                    else
                        losses.Add(Math.Abs(change));
                }

                if (gains.Count == 0) return 0;
                if (losses.Count == 0) return 100;

                decimal avgGain = gains.Average();
                decimal avgLoss = losses.Average();

                if (avgLoss == 0) return 100;

                decimal rs = avgGain / avgLoss;
                return 100 - (100 / (1 + rs));
            }
            catch
            {
                return 50;
            }
        }
    }

    public class PositionResult
    {
        public string PositionId { get; set; }
        public DateTime CloseTime { get; set; }
        public PositionType PositionType { get; set; }
        public decimal FinalProfitPercent { get; set; }
        public decimal MaxProfitPercent { get; set; }
        public decimal VolatilityAtClose { get; set; }
        public bool WasTrailingActive { get; set; }
        public decimal TrailingEfficiency { get; set; }
        public string DayOfWeek { get; set; }
    }

    public enum PositionType
    {
        Bot,
        Manual
    }

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
        
        public void UpdateStatistics(Position position, decimal currentPrice, decimal minProfitPercent)
        {
            try
            {
                if (position == null || position.EntryPrice == 0)
                    return;
                    
                CurrentPrice = currentPrice;
                Volume = Math.Abs(position.OpenVolume);
                
                decimal priceDiff = currentPrice - EntryPrice;
                if (position.Direction == Side.Sell)
                    priceDiff = -priceDiff;
                    
                ProfitCurrency = priceDiff * Volume;
                ProfitPercent = (ProfitCurrency / (EntryPrice * Volume)) * 100m;
                
                if (ProfitCurrency > MaxProfitCurrency)
                {
                    MaxProfitCurrency = ProfitCurrency;
                    MaxProfitPercent = ProfitPercent;
                }
                
                if (ProfitCurrency < MaxLossCurrency)
                {
                    MaxLossCurrency = ProfitCurrency;
                    MaxLossPercent = ProfitPercent;
                }
                
                // Расчет уровней согласно инструкции
                BreakEvenPrice = CalculateBreakEvenPrice(position);
                MinProfitPrice = CalculateMinProfitPrice(position, minProfitPercent);
            }
            catch (Exception)
            {
                // Игнорируем ошибки расчета
            }
        }
        
        private decimal CalculateBreakEvenPrice(Position position)
        {
            return position.EntryPrice;
        }
        
        private decimal CalculateMinProfitPrice(Position position, decimal minProfitPercent)
        {
            if (position.Direction == Side.Buy)
                return position.EntryPrice * (1 + minProfitPercent / 100m);
            else
                return position.EntryPrice * (1 - minProfitPercent / 100m);
        }
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

    public class DataFileInfo
    {
        public string FilePath { get; set; }
        public string Symbol { get; set; }
        public string TimeFrame { get; set; }
        public DateTime LastModified { get; set; }
        public long FileSize { get; set; }
        public int CandleCount { get; set; }
        public DateTime FirstCandleDate { get; set; }
        public DateTime LastCandleDate { get; set; }
        public int QualityScore { get; set; }
        public string Format { get; set; }
        public bool IsValid { get; set; }
        public string ValidationErrors { get; set; }
    }

    public class BatchOptimizationTask
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public List<string> Symbols { get; set; } = new List<string>();
        public List<string> TimeFrames { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public OptimizationStatus Status { get; set; } = OptimizationStatus.Pending;
        public int CompletedSymbols { get; set; }
        public int TotalSymbols { get; set; }
        public Dictionary<string, double[]> BestParameters { get; set; } = new Dictionary<string, double[]>();
        public Dictionary<string, OptimizationResult> Results { get; set; } = new Dictionary<string, OptimizationResult>();
        
        public enum OptimizationStatus
        {
            Pending,
            Running,
            Completed,
            Failed,
            Cancelled
        }
    }

    public class OptimizationResult
    {
        public string Symbol { get; set; }
        public string TimeFrame { get; set; }
        public double BestFitness { get; set; }
        public double[] BestParameters { get; set; }
        public DateTime OptimizationTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int Iterations { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
    }

    public class DataQualityReport
    {
        public DateTime ReportTime { get; set; }
        public int TotalFiles { get; set; }
        public int ValidFiles { get; set; }
        public int InvalidFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public long TotalCandles { get; set; }
        public DateTime OldestData { get; set; }
        public DateTime NewestData { get; set; }
        public Dictionary<string, int> SymbolsByTimeframe { get; set; } = new Dictionary<string, int>();
        public List<DataFileInfo> ProblemFiles { get; set; } = new List<DataFileInfo>();
        public double AverageQualityScore { get; set; }
        
        public string GetSummary()
        {
            return $"📊 Отчет качества данных ({ReportTime:yyyy-MM-dd HH:mm:ss})\n" +
                   $"📁 Файлов: {TotalFiles} (✓{ValidFiles} ✗{InvalidFiles})\n" +
                   $"📈 Свечей: {TotalCandles:N0}\n" +
                   $"💾 Размер: {FormatBytes(TotalSizeBytes)}\n" +
                   $"📅 Период: {OldestData:yyyy-MM-dd} - {NewestData:yyyy-MM-dd}\n" +
                   $"⭐ Качество: {AverageQualityScore:F1}/100";
        }
        
        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:F2} {suffixes[counter]}";
        }
    }

    public class ScannerDataExporter
    {
        private readonly BotPanel _bot;
        private readonly string _exportPath;
        
        public ScannerDataExporter(BotPanel bot, string basePath)
        {
            _bot = bot;
            _exportPath = Path.Combine(basePath, "Exports");
            
            if (!Directory.Exists(_exportPath))
            {
                Directory.CreateDirectory(_exportPath);
            }
        }
        
        public string ExportCurrentData(string symbol = null, string timeframe = null)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"scanner_data_{timestamp}.csv";
                string filePath = Path.Combine(_exportPath, fileName);
                
                var csvLines = new List<string>
                {
                    "Symbol,TimeFrame,Parameter,Value,Description"
                };
                
                AddParameter(csvLines, "IchimokuTenkan", "9", "Период Тенкан-сена");
                AddParameter(csvLines, "IchimokuKijun", "26", "Период Киджун-сена");
                AddParameter(csvLines, "IchimokuSenkouB", "52", "Период Сенкоу В");
                AddParameter(csvLines, "RSI_Period", "14", "Период RSI");
                AddParameter(csvLines, "EMA1", "300", "Быстрая EMA");
                AddParameter(csvLines, "EMA2", "80", "Средняя EMA");
                AddParameter(csvLines, "EMA3", "30", "Медленная EMA");
                AddParameter(csvLines, "TakeProfit_Long", "0.5", "Тейк-профит для лонгов");
                AddParameter(csvLines, "TakeProfit_Short", "0.3", "Тейк-профит для шортов");
                AddParameter(csvLines, "MinProfitPercent", "0.45", "Минимальная прибыль %");
                AddParameter(csvLines, "DistanceBetweenOrders", "0.3", "Расстояние между ордерами %");
                
                csvLines.Add("OPTIMIZATION,,,,");
                AddParameter(csvLines, "PSO_Population", "50", "Размер популяции PSO");
                AddParameter(csvLines, "PSO_Iterations", "100", "Максимальное число итераций");
                AddParameter(csvLines, "GA_Population", "100", "Размер популяции GA");
                AddParameter(csvLines, "GA_Generations", "100", "Число поколений GA");
                
                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                
                _bot.SendNewLogMessage($"✅ Данные скринера экспортированы: {filePath}", 
                                      LogMessageType.System);
                
                return filePath;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка экспорта данных: {ex.Message}", 
                                      LogMessageType.Error);
                return null;
            }
        }
        
        private void AddParameter(List<string> csvLines, string parameter, string value, string description)
        {
            csvLines.Add($",,{parameter},{value},{description}");
        }
        
        public string ExportOptimizationResults(List<OptimizationResult> results)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"optimization_results_{timestamp}.csv";
                string filePath = Path.Combine(_exportPath, fileName);
                
                var csvLines = new List<string>
                {
                    "Symbol,TimeFrame,Fitness,Parameters,Duration,Iterations,Timestamp"
                };
                
                foreach (var result in results)
                {
                    string parameters = string.Join(";", result.BestParameters.Select(p => p.ToString("F4")));
                    csvLines.Add(
                        $"{result.Symbol}," +
                        $"{result.TimeFrame}," +
                        $"{result.BestFitness:F4}," +
                        $"\"{parameters}\"," +
                        $"{result.Duration:hh\\:mm\\:ss}," +
                        $"{result.Iterations}," +
                        $"{result.OptimizationTime:yyyy-MM-dd HH:mm:ss}");
                }
                
                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                
                _bot.SendNewLogMessage($"✅ Результаты оптимизации экспортированы: {filePath}", 
                                      LogMessageType.System);
                
                return filePath;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка экспорта результатов: {ex.Message}", 
                                      LogMessageType.Error);
                return null;
            }
        }
        
        public string ExportDataQualityReport(DataQualityReport report)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"data_quality_{timestamp}.csv";
                string filePath = Path.Combine(_exportPath, fileName);
                
                var csvLines = new List<string>
                {
                    "ReportTime,TotalFiles,ValidFiles,InvalidFiles,TotalCandles,TotalSizeBytes,AverageQuality",
                    $"{report.ReportTime:yyyy-MM-dd HH:mm:ss},{report.TotalFiles},{report.ValidFiles},{report.InvalidFiles},{report.TotalCandles},{report.TotalSizeBytes},{report.AverageQualityScore:F1}"
                };
                
                csvLines.Add("");
                csvLines.Add("Symbol,TimeFrame,FileSize,CandleCount,QualityScore,FirstDate,LastDate");
                
                foreach (var symbolInfo in report.SymbolsByTimeframe)
                {
                    csvLines.Add($"{symbolInfo.Key},N/A,0,0,100,{report.OldestData:yyyy-MM-dd},{report.NewestData:yyyy-MM-dd}");
                }
                
                csvLines.Add("");
                csvLines.Add("ProblemFiles,ErrorDescription");
                
                foreach (var problemFile in report.ProblemFiles)
                {
                    csvLines.Add($"{Path.GetFileName(problemFile.FilePath)},{problemFile.ValidationErrors}");
                }
                
                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                
                _bot.SendNewLogMessage($"✅ Отчет качества данных экспортирован: {filePath}", 
                                      LogMessageType.System);
                
                return filePath;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка экспорта отчета качества: {ex.Message}", 
                                      LogMessageType.Error);
                return null;
            }
        }
    }
    
    public class CalculationCache
    {
        private readonly Dictionary<string, (object value, DateTime expiration)> _cache = 
            new Dictionary<string, (object value, DateTime expiration)>();
        private readonly object _lock = new object();
        
        public T GetOrAdd<T>(string key, Func<T> factory, TimeSpan ttl)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var cached) && cached.expiration > DateTime.Now)
                {
                    return (T)cached.value;
                }
                
                var value = factory();
                _cache[key] = (value, DateTime.Now.Add(ttl));
                return value;
            }
        }
        
        public void Clear() => _cache.Clear();
    }
    #endregion

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
        
        public List<Candle> HistoricalData { get; set; } = new List<Candle>();

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

        public void LoadHistoricalData(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Файл исторических данных не найден: {filePath}");
                }

                HistoricalData.Clear();
                var lines = File.ReadAllLines(filePath);
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var candle = ParseHistoricalCandle(line);
                    if (candle != null)
                    {
                        HistoricalData.Add(candle);
                    }
                }

                Console.WriteLine($"Загружено {HistoricalData.Count} исторических свечей из {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки исторических данных: {ex.Message}");
                throw;
            }
        }

        private Candle ParseHistoricalCandle(string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 7)
                    return null;

                string dateStr = parts[0];
                string timeStr = parts[1];
                
                int year = int.Parse(dateStr.Substring(0, 4));
                int month = int.Parse(dateStr.Substring(4, 2));
                int day = int.Parse(dateStr.Substring(6, 2));
                
                int hour = int.Parse(timeStr.Substring(0, 2));
                int minute = int.Parse(timeStr.Substring(2, 2));
                int second = int.Parse(timeStr.Substring(4, 2));
                
                var time = new DateTime(year, month, day, hour, minute, second);
                
                decimal open = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                decimal high = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                decimal low = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                decimal close = decimal.Parse(parts[5], CultureInfo.InvariantCulture);
                int volume = int.Parse(parts[6]);
                
                return new Candle
                {
                    TimeStart = time,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                };
            }
            catch
            {
                return null;
            }
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
        private bool _isInitialized = false;

        public void RegisterComponent(ITradingComponent component)
        {
            if (!_isInitialized)
            {
                _components.Add(component);
            }
        }

        public void Initialize(BotPanel bot)
        {
            _bot = bot;
            foreach (var component in _components)
            {
                try
                {
                    component.Initialize(bot);
                }
                catch (Exception ex)
                {
                    _bot?.SendNewLogMessage($"❌ Ошибка инициализации компонента {component.Name}: {ex.Message}", 
                                          LogMessageType.Error);
                }
            }
            _isInitialized = true;
        }

        public void Update()
        {
            foreach (var component in _components.Where(c => c.IsInitialized))
            {
                try
                {
                    component.Update();
                }
                catch (Exception ex)
                {
                    _bot?.SendNewLogMessage($"❌ Ошибка обновления компонента {component.Name}: {ex.Message}", 
                                          LogMessageType.Error);
                }
            }
        }

        public void Cleanup()
        {
            foreach (var component in _components.Where(c => c.IsInitialized))
            {
                try
                {
                    component.Cleanup();
                }
                catch (Exception ex)
                {
                    _bot?.SendNewLogMessage($"❌ Ошибка очистки компонента {component.Name}: {ex.Message}", 
                                          LogMessageType.Error);
                }
            }
            _components.Clear();
            _isInitialized = false;
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

        public List<Candle> HistoricalData { get; set; } = new List<Candle>();

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

        public void LoadHistoricalData(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Файл исторических данных не найден: {filePath}");
                }

                HistoricalData.Clear();
                var lines = File.ReadAllLines(filePath);
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var candle = ParseHistoricalCandle(line);
                    if (candle != null)
                    {
                        HistoricalData.Add(candle);
                    }
                }

                Console.WriteLine($"Загружено {HistoricalData.Count} исторических свечей из {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки исторических данных: {ex.Message}");
                throw;
            }
        }

        private Candle ParseHistoricalCandle(string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 7)
                    return null;

                string dateStr = parts[0];
                string timeStr = parts[1];
                
                int year = int.Parse(dateStr.Substring(0, 4));
                int month = int.Parse(dateStr.Substring(4, 2));
                int day = int.Parse(dateStr.Substring(6, 2));
                
                int hour = int.Parse(timeStr.Substring(0, 2));
                int minute = int.Parse(timeStr.Substring(2, 2));
                int second = int.Parse(timeStr.Substring(4, 2));
                
                var time = new DateTime(year, month, day, hour, minute, second);
                
                decimal open = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                decimal high = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                decimal low = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                decimal close = decimal.Parse(parts[5], CultureInfo.InvariantCulture);
                int volume = int.Parse(parts[6]);
                
                return new Candle
                {
                    TimeStart = time,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                };
            }
            catch
            {
                return null;
            }
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

    #region TRAILING STOP LEARNER
    public class TrailingStopLearner : ITradingComponent
    {
        public string Name => "Trailing Stop Learner";
        public bool IsInitialized { get; private set; }

        private readonly List<PositionResult> _positionHistory = new List<PositionResult>();
        private readonly object _historyLock = new object();
        private readonly BotPanel _bot;
        
        private double _successMultiplier = 1.2;
        private double _failureMultiplier = 0.8;
        private double _dayBonus = 1.1;
        private double _dayPenalty = 0.9;
        
        private readonly Dictionary<string, double> _dayMultipliers = new Dictionary<string, double>
        {
            { "Monday", 1.0 },
            { "Tuesday", 1.0 },
            { "Wednesday", 1.0 },
            { "Thursday", 1.0 },
            { "Friday", 1.0 },
            { "Saturday", 1.0 },
            { "Sunday", 1.0 }
        };

        private double _currentMultiplier = 1.0;
        private const int MAX_HISTORY_SIZE = 5000;

        private string _dataPath;

        public TrailingStopLearner(BotPanel bot, string dataPath = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _dataPath = dataPath ?? GetDefaultDataPath();
        }

        private string GetDefaultDataPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string dataPath = Path.Combine(basePath, "Data", "AI_Optimization");
            
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            
            return dataPath;
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;
            _bot.SendNewLogMessage($"✅ TrailingStopLearner инициализирован. Путь данных: {_dataPath}", LogMessageType.System);
        }

        public void Update()
        {
            UpdateLearningParameters();
        }

        public void Cleanup()
        {
            lock (_historyLock)
            {
                _positionHistory.Clear();
            }
        }

        public void OnPositionClosed(PositionResult result)
        {
            if (result == null) return;

            try
            {
                lock (_historyLock)
                {
                    if (result.MaxProfitPercent != 0)
                    {
                        result.TrailingEfficiency = (result.FinalProfitPercent / result.MaxProfitPercent) * 100;
                    }
                    else
                    {
                        result.TrailingEfficiency = 0;
                    }

                    result.DayOfWeek = result.CloseTime.ToString("dddd");
                    _positionHistory.Add(result);

                    if (_positionHistory.Count > MAX_HISTORY_SIZE)
                    {
                        _positionHistory.RemoveRange(0, _positionHistory.Count - MAX_HISTORY_SIZE);
                    }

                    AnalyzeDayOfWeekPatterns();
                    UpdateCurrentMultiplier();
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка обработки позиции для обучения: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        public double GetCurrentMultiplier(string dayOfWeek = null)
        {
            lock (_historyLock)
            {
                if (dayOfWeek != null && _dayMultipliers.ContainsKey(dayOfWeek))
                {
                    return _currentMultiplier * _dayMultipliers[dayOfWeek];
                }
                return _currentMultiplier;
            }
        }

        private void UpdateLearningParameters()
        {
            lock (_historyLock)
            {
                if (_positionHistory.Count < 10) return;

                try
                {
                    var recentPositions = _positionHistory
                        .Where(p => p.WasTrailingActive)
                        .TakeLast(100)
                        .ToList();

                    if (recentPositions.Count >= 5)
                    {
                        double avgEfficiency = recentPositions.Average(p => (double)p.TrailingEfficiency);
                        
                        if (avgEfficiency > 70)
                        {
                            _currentMultiplier *= _successMultiplier;
                        }
                        else if (avgEfficiency < 30)
                        {
                            _currentMultiplier *= _failureMultiplier;
                        }

                        _currentMultiplier = Math.Max(0.5, Math.Min(2.0, _currentMultiplier));
                    }
                }
                catch (Exception ex)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка обновления параметров обучения: {ex.Message}", 
                                          LogMessageType.Error);
                }
            }
        }

        private void AnalyzeDayOfWeekPatterns()
        {
            lock (_historyLock)
            {
                try
                {
                    foreach (var day in _dayMultipliers.Keys.ToList())
                    {
                        var dayPositions = _positionHistory
                            .Where(p => p.DayOfWeek == day && p.WasTrailingActive)
                            .ToList();

                        if (dayPositions.Count >= 10)
                        {
                            double avgEfficiency = dayPositions.Average(p => (double)p.TrailingEfficiency);
                            
                            if (avgEfficiency > 70)
                            {
                                _dayMultipliers[day] = _dayBonus;
                            }
                            else if (avgEfficiency < 30)
                            {
                                _dayMultipliers[day] = _dayPenalty;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка анализа паттернов по дням: {ex.Message}", 
                                          LogMessageType.Error);
                }
            }
        }

        private void UpdateCurrentMultiplier()
        {
            lock (_historyLock)
            {
                try
                {
                    if (_positionHistory.Count < 20) return;

                    var trailingPositions = _positionHistory
                        .Where(p => p.WasTrailingActive)
                        .TakeLast(200)
                        .ToList();

                    if (trailingPositions.Count >= 10)
                    {
                        double avgEfficiency = trailingPositions.Average(p => (double)p.TrailingEfficiency);
                        
                        if (avgEfficiency > 75)
                        {
                            _currentMultiplier *= 1.05;
                        }
                        else if (avgEfficiency < 25)
                        {
                            _currentMultiplier *= 0.95;
                        }
                        else if (avgEfficiency > 60)
                        {
                            _currentMultiplier *= 1.02;
                        }
                        else if (avgEfficiency < 40)
                        {
                            _currentMultiplier *= 0.98;
                        }

                        _currentMultiplier = Math.Max(0.3, Math.Min(3.0, _currentMultiplier));
                    }
                }
                catch (Exception ex)
                {
                    _bot.SendNewLogMessage($"❌ Ошибка обновления множителя: {ex.Message}", 
                                          LogMessageType.Error);
                }
            }
        }

        public string GetStatusSummary()
        {
            lock (_historyLock)
            {
                return $"📊 TrailingStopLearner: " +
                       $"История: {_positionHistory.Count} | " +
                       $"Множитель: {_currentMultiplier:F2} | " +
                       $"Эффективность: {CalculateAverageEfficiency():F1}%";
            }
        }

        private double CalculateAverageEfficiency()
        {
            lock (_historyLock)
            {
                var trailingPositions = _positionHistory
                    .Where(p => p.WasTrailingActive && p.TrailingEfficiency > 0)
                    .TakeLast(100)
                    .ToList();

                return trailingPositions.Any() ? trailingPositions.Average(p => (double)p.TrailingEfficiency) : 0;
            }
        }

        public void UpdateHyperparameters(double successMultiplier, double failureMultiplier, 
                                         double dayBonus, double dayPenalty)
        {
            lock (_historyLock)
            {
                _successMultiplier = Math.Max(1.0, Math.Min(2.0, successMultiplier));
                _failureMultiplier = Math.Max(0.5, Math.Min(1.0, failureMultiplier));
                _dayBonus = Math.Max(1.0, Math.Min(2.0, dayBonus));
                _dayPenalty = Math.Max(0.5, Math.Min(1.0, dayPenalty));
            }
        }

        public Dictionary<string, double> GetLearningMetrics()
        {
            lock (_historyLock)
            {
                return new Dictionary<string, double>
                {
                    { "HistorySize", _positionHistory.Count },
                    { "CurrentMultiplier", _currentMultiplier },
                    { "AvgEfficiency", CalculateAverageEfficiency() },
                    { "SuccessMultiplier", _successMultiplier },
                    { "FailureMultiplier", _failureMultiplier }
                };
            }
        }

        public string GetHistoricalDataPath(string symbol = "")
        {
            return Path.Combine(_dataPath, "HistoricalData", $"{symbol}_M1.txt");
        }

        public bool HasHistoricalData(string symbol = "")
        {
            string filePath = GetHistoricalDataPath(symbol);
            return File.Exists(filePath);
        }

        public List<string> GetAvailableHistoricalSymbols()
        {
            var symbols = new List<string>();
            string histPath = Path.Combine(_dataPath, "HistoricalData");
            
            if (Directory.Exists(histPath))
            {
                var files = Directory.GetFiles(histPath, "*.txt");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string symbol = Path.GetFileNameWithoutExtension(fileName);
                    symbols.Add(symbol);
                }
            }
            
            return symbols;
        }
    }
    #endregion

    #region TRAILING STOP COMPONENT
    public class TrailingStopComponent : ITradingComponent
    {
        public string Name => "Trailing Stop Component";
        public bool IsInitialized { get; private set; }

        private BotPanel _bot;
        private TrailingStopLearner _trailingLearner;
        private double _currentTrailingMultiplier = 1.0;
        private bool _enableGlobalLearning = false;

        public TrailingStopComponent(BotPanel bot, TrailingStopLearner learner = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _trailingLearner = learner;
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;
            
            if (_trailingLearner != null)
            {
                _currentTrailingMultiplier = _trailingLearner.GetCurrentMultiplier();
            }
            
            _bot.SendNewLogMessage($"✅ TrailingStopComponent инициализирован. Множитель: {_currentTrailingMultiplier:F2}", 
                                  LogMessageType.System);
        }

        public void Update()
        {
            if (_trailingLearner != null && _enableGlobalLearning)
            {
                _currentTrailingMultiplier = _trailingLearner.GetCurrentMultiplier();
            }
        }

        public void Cleanup()
        {
        }

        public decimal CalculateAdaptiveTrailingDistance(decimal baseDistancePercent, string dayOfWeek = null)
        {
            try
            {
                decimal adaptiveDistance = baseDistancePercent;
                
                if (_enableGlobalLearning && _trailingLearner != null)
                {
                    double multiplier = _trailingLearner.GetCurrentMultiplier(dayOfWeek);
                    adaptiveDistance = baseDistancePercent * (decimal)multiplier;
                    
                    if (Math.Abs((double)adaptiveDistance - (double)baseDistancePercent) > 0.01)
                    {
                        _bot.SendNewLogMessage(
                            $"🔄 Адаптивный трейлинг: {baseDistancePercent:F2}% → {adaptiveDistance:F2}% " +
                            $"(×{multiplier:F2})",
                            LogMessageType.System);
                    }
                }

                adaptiveDistance = Math.Max(baseDistancePercent * 0.3m, Math.Min(baseDistancePercent * 3.0m, adaptiveDistance));
                
                return adaptiveDistance;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка расчета адаптивного трейлинга: {ex.Message}", 
                                      LogMessageType.Error);
                return baseDistancePercent;
            }
        }

        public void SetGlobalLearning(bool enabled)
        {
            _enableGlobalLearning = enabled;
            
            _bot.SendNewLogMessage(
                _enableGlobalLearning 
                    ? "✅ Глобальное обучение трейлинг-стопа ВКЛЮЧЕНО" 
                    : "⚠️ Глобальное обучение трейлинг-стопа ВЫКЛЮЧЕНО",
                LogMessageType.System);
        }

        public string GetLearningStatus()
        {
            if (!_enableGlobalLearning)
                return "Глобальное обучение: ВЫКЛ";
                
            if (_trailingLearner == null)
                return "TrailingLearner не инициализирован";
                
            return $"Глобальное обучение: ВКЛ | {_trailingLearner.GetStatusSummary()}";
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
        
        private readonly ConcurrentDictionary<string, decimal> _peakProfit = 
            new ConcurrentDictionary<string, decimal>();
        
        // Для подхвата ручных позиций
        private readonly ConcurrentDictionary<string, Position> _botOpenedPositions = 
            new ConcurrentDictionary<string, Position>();
        private readonly ConcurrentDictionary<string, Position> _allPositions = 
            new ConcurrentDictionary<string, Position>();
        
        // Режим закрытия позиций
        private string _positionCloseMode = "По отдельным сделкам";
        private decimal _minProfitPercent = 0.45m;
        
        private readonly CalculationCache _cache = new CalculationCache();
        private readonly List<string> _manuallyCapturedPositions = new List<string>();

        public PositionManager() { }

        public void Initialize(BotPanel bot)
        {
            _bot = bot;
            
            Task.Run(() => InitializeExistingPositions());
            
            IsInitialized = true;
            _bot.SendNewLogMessage($"✅ {Name} инициализирован", LogMessageType.System);
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
            _botOpenedPositions.Clear();
            _allPositions.Clear();
            _manuallyCapturedPositions.Clear();
        }

        private async Task InitializeExistingPositions()
        {
            try
            {
                await Task.Delay(3000);
                
                _bot.SendNewLogMessage("🔍 Поиск существующих позиций при старте...", 
                                      LogMessageType.System);
                
                // Здесь должен быть код для получения всех открытых позиций
                // В реальной реализации нужно получить позиции из всех вкладок
                
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
            try
            {
                foreach (var positionId in _allPositions.Keys.ToList())
                {
                    if (_allPositions.TryGetValue(positionId, out var position))
                    {
                        if (position.State != PositionStateType.Open)
                            continue;
                            
                        var tab = GetTabForPosition(position);
                        if (tab == null) continue;
                        
                        decimal currentPrice = GetCurrentPrice(tab);
                        UpdatePositionPrice(positionId, currentPrice);
                    }
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка обновления статистики позиций: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        private BotTabSimple GetTabForPosition(Position position)
        {
            // В реальной реализации нужно найти вкладку по имени инструмента
            return null;
        }

        private decimal GetCurrentPrice(BotTabSimple tab)
        {
            if (tab == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
                return 0;
                
            return tab.CandlesAll.Last().Close;
        }

        public bool CanClosePosition(Position position, BotTabSimple tab = null, decimal currentPrice = 0)
        {
            if (position == null || position.State != PositionStateType.Open) 
                return false;

            try
            {
                string positionId = GetPositionId(position);
                
                // Получаем текущую прибыль
                decimal profit = CalculatePositionProfit(position, tab, currentPrice);
                decimal entryPrice = position.EntryPrice;
                
                if (entryPrice == 0) return false;

                decimal profitPercent = (profit / (entryPrice * Math.Abs(position.OpenVolume))) * 100m;
                decimal requiredProfit = entryPrice * (_minProfitPercent / 100m) * Math.Abs(position.OpenVolume);

                // АБСОЛЮТНАЯ ЗАЩИТА ОТ УБЫТКОВ - НИКОГДА НЕ ЗАКРЫВАЕМ С УБЫТКОМ
                if (profit < 0)
                {
                    LogPositionBlocked(position, profit, requiredProfit, profitPercent, _minProfitPercent);
                    return false;
                }

                // Проверяем минимальную прибыль
                if (profit < requiredProfit)
                {
                    LogPositionBlocked(position, profit, requiredProfit, profitPercent, _minProfitPercent);
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

        private string GetPositionId(Position position)
        {
            return position.Number.ToString();
        }

        private decimal CalculatePositionProfit(Position position, BotTabSimple tab, decimal currentPrice)
        {
            try
            {
                if (position == null) return 0;

                if (currentPrice == 0 && tab != null && tab.CandlesAll != null && tab.CandlesAll.Count > 0)
                {
                    // ✅ ИСПРАВЛЕНИЕ: Безопасный доступ к Last()
                    currentPrice = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;
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
            string positionType = IsBotPosition(position) ? "БОТ" : "РУЧНАЯ";
            
            _bot.SendNewLogMessage(
                $"⛔ БЛОКИРОВКА ЗАКРЫТИЯ: Позиция #{GetPositionId(position)} | " +
                $"Тип: {positionType} | " +
                $"Прибыль: {profit:F2} ({profitPercent:F2}%) | " +
                $"Требуется: {requiredProfit:F2} ({minProfitPercent}%) | " +
                $"Направление: {position.Direction}",
                LogMessageType.System);
        }

        public void RegisterPosition(Position position, PositionType type)
        {
            if (position == null) return;

            string positionId = GetPositionId(position);
            
            var stats = new PositionStatistics
            {
                PositionId = positionId,
                Type = type,
                EntryPrice = position.EntryPrice,
                CurrentPrice = position.EntryPrice,
                Volume = Math.Abs(position.OpenVolume)
            };

            _positionStats[positionId] = stats;
            
            if (type == PositionType.Bot)
            {
                _botOpenedPositions[positionId] = position;
            }
            
            _allPositions[positionId] = position;
            
            LogPositionRegistered(position, type);
        }

        private void LogPositionRegistered(Position position, PositionType type)
        {
            string positionType = type == PositionType.Bot ? "БОТ" : "РУЧНАЯ";
            
            _bot.SendNewLogMessage(
                $"✅ {positionType} ПОЗИЦИЯ #{GetPositionId(position)} ЗАРЕГИСТРИРОВАНА\n" +
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
            if (position == null) return false;
            string positionId = GetPositionId(position);
            return _botOpenedPositions.ContainsKey(positionId);
        }

        public List<PositionStatistics> GetAllPositionStatistics()
        {
            return _positionStats.Values.ToList();
        }

        public void UpdatePositionPrice(string positionId, decimal currentPrice)
        {
            if (_positionStats.TryGetValue(positionId, out var stats) && 
                _allPositions.TryGetValue(positionId, out var position))
            {
                stats.UpdateStatistics(position, currentPrice, _minProfitPercent);
            }
        }

        private decimal CalculateBreakEvenPrice(Position position)
        {
            return position.EntryPrice;
        }

        private decimal CalculateMinProfitPrice(Position position, decimal minProfitPercent)
        {
            if (position.Direction == Side.Buy)
            {
                return position.EntryPrice * (1 + minProfitPercent / 100m);
            }
            else
            {
                return position.EntryPrice * (1 - minProfitPercent / 100m);
            }
        }

        public void CaptureManualPosition(Position position)
        {
            if (position == null) return;
            
            string positionId = GetPositionId(position);
            
            if (_manuallyCapturedPositions.Contains(positionId))
                return;
                
            _manuallyCapturedPositions.Add(positionId);

            // Проверяем, не является ли позиция уже ботовской
            if (!_botOpenedPositions.ContainsKey(positionId))
            {
                RegisterPosition(position, PositionType.Manual);
                
                // Детальное логирование подхвата согласно инструкции
                decimal currentPrice = position.EntryPrice;
                decimal profit = CalculatePositionProfit(position, null, currentPrice);
                decimal minProfitPrice = CalculateMinProfitPrice(position, _minProfitPercent);
                
                _bot.SendNewLogMessage(
                    $"🔄 РУЧНАЯ ПОЗИЦИЯ #{positionId} ПОДХВАЧЕНА ДЛЯ СОПРОВОЖДЕНИЯ\n" +
                    $"Инструмент: {position.SecurityName}\n" +
                    $"Направление: {position.Direction}\n" +
                    $"Цена входа: {position.EntryPrice:F4}\n" +
                    $"Объем: {Math.Abs(position.OpenVolume):F2}\n" +
                    $"Текущая прибыль: {profit:F2}\n" +
                    $"Минимальная цена прибыли: {minProfitPrice:F4} ({_minProfitPercent}%)\n" +
                    $"Режим сопровождения: {_positionCloseMode}",
                    LogMessageType.System);
            }
        }

        public int GetBotPositionsCount()
        {
            return _botOpenedPositions.Count;
        }

        public int GetManualPositionsCount()
        {
            return _allPositions.Count - _botOpenedPositions.Count;
        }

        public List<Position> GetAllPositions()
        {
            return _allPositions.Values.ToList();
        }

        public List<Position> GetBotPositions()
        {
            return _botOpenedPositions.Values.ToList();
        }

        public List<Position> GetManualPositions()
        {
            return _allPositions.Values
                .Where(p => !_botOpenedPositions.ContainsKey(GetPositionId(p)))
                .ToList();
        }

        public void SetPositionCloseMode(string mode)
        {
            _positionCloseMode = mode;
            _bot.SendNewLogMessage($"✅ Установлен режим закрытия: {mode}", LogMessageType.System);
        }

        public string GetPositionCloseMode()
        {
            return _positionCloseMode;
        }
        
        public void SetMinProfitPercent(decimal percent)
        {
            _minProfitPercent = percent;
            _bot.SendNewLogMessage($"✅ Установлена минимальная прибыль: {percent}%", LogMessageType.System);
        }
        
        public decimal GetMinProfitPercent()
        {
            return _minProfitPercent;
        }
        
        public string GetPositionSummary()
        {
            int botCount = GetBotPositionsCount();
            int manualCount = GetManualPositionsCount();
            decimal botTotalProfit = GetAllPositionStatistics()
                .Where(s => s.Type == PositionType.Bot)
                .Sum(s => s.ProfitCurrency);
            decimal manualTotalProfit = GetAllPositionStatistics()
                .Where(s => s.Type == PositionType.Manual)
                .Sum(s => s.ProfitCurrency);
                
            return $"📊 СТАТИСТИКА ПОЗИЦИЙ: Бот {botCount} шт. | Ручные {manualCount} шт.\n" +
                   $"📈 СУММАРНЫЙ РЕЗУЛЬТАТ: Бот {botTotalProfit:F2} | Ручные {manualTotalProfit:F2}";
        }
        
        public void LogDetailedStatistics()
        {
            var allStats = GetAllPositionStatistics();
            
            _bot.SendNewLogMessage("📊 ===== ДЕТАЛЬНАЯ СТАТИСТИКА ПОЗИЦИЙ =====", LogMessageType.System);
            _bot.SendNewLogMessage(GetPositionSummary(), LogMessageType.System);
            
            foreach (var stat in allStats.Where(s => s.Type == PositionType.Bot))
            {
                _bot.SendNewLogMessage(
                    $"🤖 БОТОВСКАЯ ПОЗИЦИЯ #{stat.PositionId}:\n" +
                    $"   Цена входа: {stat.EntryPrice:F4}\n" +
                    $"   Текущая цена: {stat.CurrentPrice:F4}\n" +
                    $"   Прибыль: {stat.ProfitCurrency:F2} ({stat.ProfitPercent:F2}%)\n" +
                    $"   Макс. прибыль: {stat.MaxProfitCurrency:F2} ({stat.MaxProfitPercent:F2}%)\n" +
                    $"   Макс. убыток: {stat.MaxLossCurrency:F2} ({stat.MaxLossPercent:F2}%)\n" +
                    $"   Уровень безубытка: {stat.BreakEvenPrice:F4}\n" +
                    $"   Мин. цена прибыли: {stat.MinProfitPrice:F4}",
                    LogMessageType.System);
            }
            
            foreach (var stat in allStats.Where(s => s.Type == PositionType.Manual))
            {
                _bot.SendNewLogMessage(
                    $"👤 РУЧНАЯ ПОЗИЦИЯ #{stat.PositionId}:\n" +
                    $"   Цена входа: {stat.EntryPrice:F4}\n" +
                    $"   Текущая цена: {stat.CurrentPrice:F4}\n" +
                    $"   Прибыль: {stat.ProfitCurrency:F2} ({stat.ProfitPercent:F2}%)\n" +
                    $"   Макс. прибыль: {stat.MaxProfitCurrency:F2} ({stat.MaxProfitPercent:F2}%)\n" +
                    $"   Макс. убыток: {stat.MaxLossCurrency:F2} ({stat.MaxLossPercent:F2}%)\n" +
                    $"   Уровень безубытка: {stat.BreakEvenPrice:F4}\n" +
                    $"   Мин. цена прибыли: {stat.MinProfitPrice:F4}",
                    LogMessageType.System);
            }
            
            _bot.SendNewLogMessage("==========================================", LogMessageType.System);
        }
    }
    #endregion

    #region ABSOLUTE LOSS PROTECTION COMPONENT
    public class AbsoluteLossProtectionComponent : ITradingComponent
    {
        public string Name => "Absolute Loss Protection Component";
        public bool IsInitialized { get; private set; }
        
        private BotPanel _bot;
        private PositionManager _positionManager;
        private decimal _minProfitPercent = 0.45m;
        
        public AbsoluteLossProtectionComponent(BotPanel bot, PositionManager positionManager)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;
            _bot.SendNewLogMessage($"✅ {Name} инициализирован", LogMessageType.System);
        }

        public void Update()
        {
            // Периодическая проверка защиты
        }

        public void Cleanup()
        {
        }

        public bool CanClosePosition(Position position, BotTabSimple tab = null, decimal currentPrice = 0)
        {
            return _positionManager.CanClosePosition(position, tab, currentPrice);
        }

        public void SetMinProfitPercent(decimal percent)
        {
            _minProfitPercent = percent;
            _positionManager.SetMinProfitPercent(percent);
            _bot.SendNewLogMessage($"✅ Установлена минимальная прибыль: {percent}%", LogMessageType.System);
        }

        public string GetProtectionStatus()
        {
            return $"🛡️ Абсолютная защита активна. Мин. прибыль: {_minProfitPercent}%";
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
        
        private string _dataPath;
        private List<string> _availableSymbols = new List<string>();
        private string _currentSymbol = "";
        
        public event Action<double[]> OnBestParametersUpdated;
        private PSOScreenerHybridPro _mainBot;

        public HybridAiOptimizationEngine(BotPanel bot, string dataPath = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _mainBot = bot as PSOScreenerHybridPro;
            _dataPath = dataPath ?? GetDefaultDataPath();
            _stats = new OptimizationStatistics();
        }

        private string GetDefaultDataPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string dataPath = Path.Combine(basePath, "Data", "AI_Optimization");
            
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            
            string histPath = Path.Combine(dataPath, "HistoricalData");
            if (!Directory.Exists(histPath))
            {
                Directory.CreateDirectory(histPath);
            }
            
            return dataPath;
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;
            LoadAvailableSymbols();
            
            _bot.SendNewLogMessage($"✅ {Name} инициализирован. Путь данных: {_dataPath}", 
                                  LogMessageType.System);
            
            if (_availableSymbols.Count > 0)
            {
                _bot.SendNewLogMessage($"📊 Доступно исторических данных для: {string.Join(", ", _availableSymbols.Take(10))}...", 
                                      LogMessageType.System);
            }
            else
            {
                _bot.SendNewLogMessage("⚠️ Исторические данные не найдены. Поместите файлы в Data/AI_Optimization/HistoricalData/", 
                                      LogMessageType.System);
            }
            
            StartContinuousOptimization();
        }

        public void Update()
        {
            // Непрерывная оптимизация согласно инструкции
        }

        public void Cleanup()
        {
            _cts?.Cancel();
        }

        private void StartContinuousOptimization()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            
            _cts = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (!_isOptimizing && _availableSymbols.Count > 0)
                        {
                            RunHybridOptimization();
                        }
                        
                        await Task.Delay(TimeSpan.FromMinutes(1), _cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _bot.SendNewLogMessage($"❌ Ошибка непрерывной оптимизации: {ex.Message}", 
                                              LogMessageType.Error);
                        await Task.Delay(TimeSpan.FromMinutes(5), _cts.Token);
                    }
                }
            }, _cts.Token);
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

                var psoTask = Task.Run(() => RunPsoOptimization());
                var gaTask = Task.Run(() => RunGaOptimization());

                Task.WaitAll(psoTask, gaTask);
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
                    FitnessFunction = CalculateFitnessWithBacktest
                };

                _pso.Initialize();
                _pso.RunOptimization();

                _bot.SendNewLogMessage($"✅ PSO оптимизация завершена. Лучший фитнес: {_pso.GetBestFitness():F4}", 
                                      LogMessageType.System);
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
                    FitnessFunction = CalculateFitnessWithBacktest
                };

                _ga.Initialize();
                _ga.RunOptimization();

                _bot.SendNewLogMessage($"✅ GA оптимизация завершена. Лучший фитнес: {_ga.GetBestFitness():F4}", 
                                      LogMessageType.System);
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

            // Применяем лучшие параметры к боту
            ApplyOptimizationResults(psoBest, "PSO");
            ApplyOptimizationResults(gaBest, "GA");

            OnBestParametersUpdated?.Invoke(psoBest);
            OnBestParametersUpdated?.Invoke(gaBest);

            _bot.SendNewLogMessage("🔄 Обмен лучшими решениями между PSO и GA", LogMessageType.System);
            _bot.SendNewLogMessage("✅ Лучшие параметры применены к боту!", LogMessageType.System);
        }

        private void ApplyOptimizationResults(double[] parameters, string algorithmName)
        {
            if (parameters == null || parameters.Length < 13)
                return;

            // Применяем параметры к основному боту
            var targetBot = _mainBot;
            if (targetBot == null)
                return;

            try
            {
                _bot.SendNewLogMessage($"🔧 Применение результатов {algorithmName} оптимизации к торговому боту...", LogMessageType.System);

                // Применяем оптимизированные параметры к основным параметрам бота
                int paramIndex = 0;

                // EMA периоды
                if (targetBot.Ema1Period != null && paramIndex < parameters.Length)
                {
                    int ema1Value = (int)Math.Max(100, Math.Min(500, Math.Round(parameters[paramIndex])));
                    targetBot.Ema1Period.ValueInt = ema1Value;
                    _bot.SendNewLogMessage($"📊 EMA1 период: {ema1Value}", LogMessageType.System);
                    paramIndex++;
                }

                if (targetBot.Ema2Period != null && paramIndex < parameters.Length)
                {
                    int ema2Value = (int)Math.Max(50, Math.Min(150, Math.Round(parameters[paramIndex])));
                    targetBot.Ema2Period.ValueInt = ema2Value;
                    _bot.SendNewLogMessage($"📊 EMA2 период: {ema2Value}", LogMessageType.System);
                    paramIndex++;
                }

                // RSI параметры
                if (targetBot.RsiPeriod != null && paramIndex < parameters.Length)
                {
                    int rsiPeriodValue = (int)Math.Max(10, Math.Min(20, Math.Round(parameters[paramIndex])));
                    targetBot.RsiPeriod.ValueInt = rsiPeriodValue;
                    _bot.SendNewLogMessage($"📊 RSI период: {rsiPeriodValue}", LogMessageType.System);
                    paramIndex++;
                }

                if (targetBot.RsiOverbought != null && paramIndex < parameters.Length)
                {
                    decimal rsiOverboughtValue = Math.Max(65, Math.Min(80, (decimal)parameters[paramIndex]));
                    targetBot.RsiOverbought.ValueDecimal = rsiOverboughtValue;
                    _bot.SendNewLogMessage($"📊 RSI перекупленность: {rsiOverboughtValue}", LogMessageType.System);
                    paramIndex++;
                }

                if (targetBot.RsiOversold != null && paramIndex < parameters.Length)
                {
                    decimal rsiOversoldValue = Math.Max(20, Math.Min(35, (decimal)parameters[paramIndex]));
                    targetBot.RsiOversold.ValueDecimal = rsiOversoldValue;
                    _bot.SendNewLogMessage($"📊 RSI перепроданность: {rsiOversoldValue}", LogMessageType.System);
                    paramIndex++;
                }

                // Min Profit Percent
                if (targetBot.MinProfitPercent != null && paramIndex < parameters.Length)
                {
                    decimal minProfitValue = Math.Max(0.1m, Math.Min(1.0m, (decimal)parameters[paramIndex]));
                    targetBot.MinProfitPercent.ValueDecimal = minProfitValue;
                    _bot.SendNewLogMessage($"📊 Min Profit Percent: {minProfitValue}%", LogMessageType.System);
                    paramIndex++;
                }

                // Distance Between Orders
                if (targetBot.DistanceBetweenOrders != null && paramIndex < parameters.Length)
                {
                    decimal distanceValue = Math.Max(0.1m, Math.Min(1.0m, (decimal)parameters[paramIndex]));
                    targetBot.DistanceBetweenOrders.ValueDecimal = distanceValue;
                    _bot.SendNewLogMessage($"📊 Distance Between Orders: {distanceValue}%", LogMessageType.System);
                    paramIndex++;
                }

                _bot.SendNewLogMessage($"✅ Параметры {algorithmName} оптимизации применены к торговому боту!", LogMessageType.System);

                // ДОБАВЛЕНИЕ ЭКСПОРТА РЕЗУЛЬТАТОВ
                ExportOptimizationResults(parameters, algorithmName);

            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка применения результатов оптимизации {algorithmName}: {ex.Message}", LogMessageType.Error);
            }
        }

        private void ExportOptimizationResults(double[] parameters, string algorithmName)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"optimization_applied_{algorithmName}_{timestamp}.csv";
                string filePath = Path.Combine(_dataPath, "OptimizationResults", fileName);

                var csvLines = new List<string>
                {
                    "Parameter,Value,Description",
                    $"Algorithm,{algorithmName},Оптимизационный алгоритм",
                    $"Timestamp,{DateTime.Now:yyyy-MM-dd HH:mm:ss},Время применения",
                    "EMA1_Period," + (parameters.Length > 0 ? parameters[0].ToString("F2") : "N/A") + ",Быстрая EMA",
                    "EMA2_Period," + (parameters.Length > 1 ? parameters[1].ToString("F2") : "N/A") + ",Средняя EMA",
                    "RSI_Period," + (parameters.Length > 2 ? parameters[2].ToString("F2") : "N/A") + ",Период RSI",
                    "RSI_Overbought," + (parameters.Length > 3 ? parameters[3].ToString("F2") : "N/A") + ",Уровень перекупленности RSI",
                    "RSI_Oversold," + (parameters.Length > 4 ? parameters[4].ToString("F2") : "N/A") + ",Уровень перепроданности RSI",
                    "Min_Profit_Percent," + (parameters.Length > 5 ? parameters[5].ToString("F4") : "N/A") + ",Минимальная прибыль %",
                    "Distance_Between_Orders," + (parameters.Length > 6 ? parameters[6].ToString("F4") : "N/A") + ",Расстояние между ордерами %"
                };

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);

                _bot.SendNewLogMessage($"💾 Результаты оптимизации {algorithmName} экспортированы: {filePath}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка экспорта результатов оптимизации: {ex.Message}", LogMessageType.Error);
            }
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
        { "RsiOverbought", (65, 80) },
                { "RsiOversold", (20, 35) },
                { "MinProfitPercent", (0.1, 1.0) },
                { "Ema1Period", (200, 400) },
                { "Ema2Period", (70, 90) },
                { "Ema3Period", (25, 35) }
            };
        }

        private double CalculateFitnessWithBacktest(double[] parameters)
        {
            try
            {
                if (parameters == null || parameters.Length == 0)
                    return double.MaxValue;

                var historicalData = _pso?.HistoricalData ?? _ga?.HistoricalData;
                if (historicalData == null || historicalData.Count < 100)
                {
                    return CalculateSimpleFitness(parameters);
                }

                var backtestResult = RunBacktest(parameters, historicalData);
                double fitness = CalculateFitnessFromBacktest(backtestResult);

                return fitness;
            }
            catch
            {
                return double.MaxValue;
            }
        }

        private double CalculateSimpleFitness(double[] parameters)
        {
            // УЛУЧШЕННАЯ ПРОСТАЯ ФИТНЕС-ФУНКЦИЯ
            // Оцениваем параметры на основе их разумности для стратегии
            if (parameters.Length < 7) return double.MaxValue;

            double fitness = 0;

            // EMA периоды - предпочитаем разумные значения
            double ema1Period = parameters[0];
            double ema2Period = parameters[1];
            if (ema1Period > ema2Period && ema1Period > 50 && ema1Period < 500) fitness += 10;
            if (ema2Period > 30 && ema2Period < 200) fitness += 5;

            // RSI период - должен быть разумным
            double rsiPeriod = parameters[2];
            if (rsiPeriod >= 10 && rsiPeriod <= 20) fitness += 8;

            // RSI уровни - должны быть в правильном диапазоне
            double rsiOverbought = parameters[3];
            double rsiOversold = parameters[4];
            if (rsiOverbought > rsiOversold && rsiOverbought <= 80 && rsiOversold >= 20) fitness += 6;

            // Min Profit Percent - должен быть положительным и разумным
            double minProfit = parameters[5];
            if (minProfit > 0.05 && minProfit < 2.0) fitness += 5;

            // Distance Between Orders - должен быть положительным
            double distance = parameters[6];
            if (distance > 0.05 && distance < 2.0) fitness += 4;

            // Штрафы за экстремальные значения
            if (ema1Period > 1000 || ema2Period > 500) fitness -= 20;
            if (rsiPeriod < 5 || rsiPeriod > 50) fitness -= 10;
            if (minProfit > 5.0) fitness -= 15;

            return fitness;
        }

        public class BacktestResult
        {
            public int TotalTrades { get; set; }
            public int WinningTrades { get; set; }
            public int LosingTrades { get; set; }
            public double WinRate { get; set; }
            public double TotalProfit { get; set; }
            public double TotalLoss { get; set; }
            public double NetProfit { get; set; }
            public double ProfitFactor { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            public double AverageTrade { get; set; }
            public double RecoveryFactor { get; set; }
        }

        private BacktestResult RunBacktest(double[] parameters, List<Candle> historicalData)
        {
            var result = new BacktestResult();
            
            try
            {
                decimal balance = 10000m;
                decimal maxBalance = balance;
                decimal minBalance = balance;
                List<decimal> returns = new List<decimal>();
                
                for (int i = 50; i < historicalData.Count - 1; i++)
                {
                    var currentCandle = historicalData[i];
                    var nextCandle = historicalData[i + 1];
                    
                    decimal priceChange = (nextCandle.Close - currentCandle.Close) / currentCandle.Close * 100;
                    
                    if (priceChange > 0.1m)
                    {
                        decimal tradeResult = priceChange * 0.01m * 100;
                        balance += tradeResult;
                        result.TotalTrades++;
                        
                        if (tradeResult > 0)
                            result.WinningTrades++;
                        else
                            result.LosingTrades++;
                        
                        returns.Add((decimal)tradeResult);
                    }
                    
                    if (balance > maxBalance)
                        maxBalance = balance;
                    if (balance < minBalance)
                        minBalance = balance;
                }
                
                if (result.TotalTrades > 0)
                {
                    result.WinRate = (double)result.WinningTrades / result.TotalTrades * 100;
                    result.NetProfit = (double)(balance - 10000m);
                    result.MaxDrawdown = (double)((maxBalance - minBalance) / maxBalance * 100);
                    
                    if (returns.Count > 0)
                    {
                        double avgReturn = (double)returns.Average();
                        double stdDev = Math.Sqrt(returns.Select(r => Math.Pow((double)r - avgReturn, 2)).Sum() / returns.Count);
                        result.SharpeRatio = stdDev != 0 ? avgReturn / stdDev * Math.Sqrt(252) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка бэктестинга: {ex.Message}", LogMessageType.Error);
            }
            
            return result;
        }

        private double CalculateFitnessFromBacktest(BacktestResult result)
        {
            if (result.TotalTrades == 0)
                return double.MaxValue;

            double fitness = 0;

            // Базовые метрики
            fitness += Math.Min(result.TotalTrades, 100) * 0.1;
            fitness += result.WinRate * 2;
            fitness += result.ProfitFactor * 10;
            fitness -= result.MaxDrawdown * 0.5;
            fitness += result.SharpeRatio * 5;
            fitness += result.NetProfit * 0.01;

            // Улучшенные метрики для повышения прибыльности
            fitness += result.RecoveryFactor * 3;  // Фактор восстановления
            fitness += Math.Min(result.AverageTrade * 10, 50);  // Средняя прибыль на сделку
            fitness -= (result.MaxDrawdown > 20 ? (result.MaxDrawdown - 20) * 2 : 0);  // Штраф за большую просадку

            // Бонус за стабильность
            double consistencyBonus = result.TotalTrades > 50 ? Math.Min(result.WinRate * result.ProfitFactor / 100, 10) : 0;
            fitness += consistencyBonus;

            return -fitness;
        }

        private void LoadAvailableSymbols()
        {
            try
            {
                string histPath = Path.Combine(_dataPath, "HistoricalData");
                
                if (Directory.Exists(histPath))
                {
                    var files = Directory.GetFiles(histPath, "*.txt");
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        _availableSymbols.Add(fileName);
                    }
                    
                    _availableSymbols.Sort();
                    
                    if (_availableSymbols.Count > 0)
                    {
                        _currentSymbol = _availableSymbols[0];
                    }
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка загрузки списка символов: {ex.Message}", 
                                      LogMessageType.Error);
            }
        }

        public void SetHistoricalSymbol(string symbol)
        {
            if (_availableSymbols.Contains(symbol))
            {
                _currentSymbol = symbol;
                _bot.SendNewLogMessage($"✅ Установлен символ для оптимизации: {symbol}", 
                                      LogMessageType.System);
            }
            else
            {
                _bot.SendNewLogMessage($"❌ Символ {symbol} не найден в исторических данных", 
                                      LogMessageType.Error);
            }
        }

        public List<string> GetAvailableSymbols()
        {
            return new List<string>(_availableSymbols);
        }

        public string GetCurrentSymbol()
        {
            return _currentSymbol;
        }

        public string GetDataPath()
        {
            return _dataPath;
        }

        public bool HasHistoricalData()
        {
            return _availableSymbols.Count > 0;
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
            if (_isOptimizing)
                return $"🚀 Оптимизация выполняется для {_currentSymbol}...";
            else if (HasHistoricalData())
                return $"✅ Оптимизация неактивна. Доступно символов: {_availableSymbols.Count}";
            else
                return "⚠️ Исторические данные не найдены";
        }
    }
    #endregion

    #region ГИБКИЙ ИНСТРУМЕНТ МЕНЕДЖЕР
    public class FlexibleInstrumentManager : ITradingComponent
    {
        public string Name => "Flexible Instrument Manager";
        public bool IsInitialized { get; private set; }
        
        private readonly BotPanel _bot;
        private readonly string _dataPath;
        private readonly List<DataFileInfo> _dataFiles = new List<DataFileInfo>();
        private readonly Dictionary<string, List<string>> _availableSymbolsByTimeframe = new Dictionary<string, List<string>>();
        private readonly object _dataLock = new object();
        
        private readonly ScannerDataExporter _exporter;
        private DataQualityReport _lastQualityReport;
        
        private Timer _autoScanTimer;
        private bool _isScanning = false;
        
        private readonly List<string> _supportedTimeframes = new List<string>
        {
            "M1", "M5", "M15", "M30", "H1", "H4", "D1", "W1", "MN"
        };
        
        private readonly Dictionary<string, Regex> _filePatterns = new Dictionary<string, Regex>
        {
            { "Standard", new Regex(@"^([A-Za-z0-9]+)_([A-Z]{1,3}\d{1,2})\.txt$", RegexOptions.Compiled) },
            { "Simple", new Regex(@"^([A-Za-z0-9]+)\.txt$", RegexOptions.Compiled) },
            { "WithDate", new Regex(@"^([A-Za-z0-9]+)_(\d{8})_([A-Z]{1,3}\d{1,2})\.txt$", RegexOptions.Compiled) }
        };

        public FlexibleInstrumentManager(BotPanel bot, string dataPath = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _dataPath = dataPath ?? GetDefaultDataPath();
            _exporter = new ScannerDataExporter(bot, _dataPath);
            
            InitializeTimeframeDictionary();
        }

        private string GetDefaultDataPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string dataPath = Path.Combine(basePath, "Data", "AI_Optimization");
            
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            
            string histPath = Path.Combine(dataPath, "HistoricalData");
            if (!Directory.Exists(histPath))
            {
                Directory.CreateDirectory(histPath);
            }
            
            return dataPath;
        }

        private void InitializeTimeframeDictionary()
        {
            foreach (var timeframe in _supportedTimeframes)
            {
                _availableSymbolsByTimeframe[timeframe] = new List<string>();
            }
        }

        public void Initialize(BotPanel bot)
        {
            IsInitialized = true;
            
            ScanDataFiles();
            
            _autoScanTimer = new Timer(_ => AutoScan(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            
            _bot.SendNewLogMessage($"✅ {Name} инициализирован. Путь данных: {_dataPath}", 
                                  LogMessageType.System);
            
            LogAvailableData();
        }

        public void Update()
        {
            // Периодические проверки
        }

        public void Cleanup()
        {
            _autoScanTimer?.Dispose();
            _autoScanTimer = null;
        }

        private void AutoScan()
        {
            if (_isScanning) return;
            
            lock (_dataLock)
            {
                _isScanning = true;
                try
                {
                    bool changesDetected = ScanDataFiles();
                    if (changesDetected)
                    {
                        _bot.SendNewLogMessage("🔄 Обнаружены изменения в исторических данных", 
                                              LogMessageType.System);
                        LogAvailableData();
                    }
                }
                finally
                {
                    _isScanning = false;
                }
            }
        }

        public bool ScanDataFiles()
        {
            try
            {
                lock (_dataLock)
                {
                    string histPath = Path.Combine(_dataPath, "HistoricalData");
                    if (!Directory.Exists(histPath))
                    {
                        _bot.SendNewLogMessage($"❌ Папка с данными не существует: {histPath}", 
                                              LogMessageType.Error);
                        return false;
                    }

                    var oldFileCount = _dataFiles.Count;
                    _dataFiles.Clear();
                    InitializeTimeframeDictionary();
                    
                    var txtFiles = Directory.GetFiles(histPath, "*.txt", SearchOption.AllDirectories);
                    bool changesDetected = false;
                    
                    foreach (var file in txtFiles)
                    {
                        var fileInfo = AnalyzeDataFile(file);
                        if (fileInfo != null)
                        {
                            _dataFiles.Add(fileInfo);
                            
                            if (!string.IsNullOrEmpty(fileInfo.TimeFrame) && 
                                _availableSymbolsByTimeframe.ContainsKey(fileInfo.TimeFrame))
                            {
                                if (!_availableSymbolsByTimeframe[fileInfo.TimeFrame].Contains(fileInfo.Symbol))
                                {
                                    _availableSymbolsByTimeframe[fileInfo.TimeFrame].Add(fileInfo.Symbol);
                                    changesDetected = true;
                                }
                            }
                        }
                    }
                    
                    foreach (var timeframe in _availableSymbolsByTimeframe.Keys.ToList())
                    {
                        _availableSymbolsByTimeframe[timeframe].Sort();
                    }
                    
                    _bot.SendNewLogMessage($"📊 Сканирование завершено. Найдено {_dataFiles.Count} файлов", 
                                          LogMessageType.System);
                    
                    return changesDetected || (_dataFiles.Count != oldFileCount);
                }
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка сканирования файлов: {ex.Message}", 
                                      LogMessageType.Error);
                return false;
            }
        }

        private DataFileInfo AnalyzeDataFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var dataFileInfo = new DataFileInfo
                {
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsValid = false
                };
                
                ParseFileName(Path.GetFileName(filePath), out string symbol, out string timeframe);
                dataFileInfo.Symbol = symbol;
                dataFileInfo.TimeFrame = timeframe ?? "Unknown";
                
                var validationResult = ValidateDataFile(filePath);
                dataFileInfo.IsValid = validationResult.IsValid;
                dataFileInfo.ValidationErrors = validationResult.Errors;
                dataFileInfo.QualityScore = validationResult.QualityScore;
                dataFileInfo.CandleCount = validationResult.CandleCount;
                dataFileInfo.FirstCandleDate = validationResult.FirstDate;
                dataFileInfo.LastCandleDate = validationResult.LastDate;
                dataFileInfo.Format = validationResult.Format;
                
                return dataFileInfo;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка анализа файла {filePath}: {ex.Message}", 
                                      LogMessageType.Error);
                return null;
            }
        }

        private void ParseFileName(string fileName, out string symbol, out string timeframe)
        {
            symbol = "Unknown";
            timeframe = null;
            
            foreach (var pattern in _filePatterns)
            {
                var match = pattern.Value.Match(fileName);
                if (match.Success)
                {
                    symbol = match.Groups[1].Value;
                    if (match.Groups.Count > 2)
                    {
                        timeframe = match.Groups[2].Value;
                    }
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(timeframe))
            {
                timeframe = DetectTimeframeFromFileName(fileName);
            }
        }

        private string DetectTimeframeFromFileName(string fileName)
        {
            foreach (var tf in _supportedTimeframes)
            {
                if (fileName.Contains($"_{tf}.") || fileName.Contains($"{tf}."))
                {
                    return tf;
                }
            }
            return "Unknown";
        }

        private (bool IsValid, string Errors, int QualityScore, int CandleCount, 
                DateTime FirstDate, DateTime LastDate, string Format) ValidateDataFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return (false, "File not found", 0, 0, DateTime.MinValue, DateTime.MinValue, "Unknown");
                }

                var lines = File.ReadAllLines(filePath);
                if (lines.Length == 0)
                {
                    return (false, "Empty file", 0, 0, DateTime.MinValue, DateTime.MinValue, "Unknown");
                }

                int validCandles = 0;
                DateTime firstDate = DateTime.MaxValue;
                DateTime lastDate = DateTime.MinValue;
                List<string> errors = new List<string>();
                
                string format = DetectFileFormat(lines[0]);
                
                for (int i = 0; i < Math.Min(lines.Length, 1000); i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;
                    
                    var candle = ParseCandleFromLine(lines[i], format);
                    if (candle != null)
                    {
                        validCandles++;
                        if (candle.TimeStart < firstDate) firstDate = candle.TimeStart;
                        if (candle.TimeStart > lastDate) lastDate = candle.TimeStart;
                    }
                    else
                    {
                        errors.Add($"Line {i+1}: Invalid format");
                    }
                }
                
                int qualityScore = CalculateQualityScore(lines.Length, validCandles, firstDate, lastDate);
                
                string errorString = errors.Count > 0 ? string.Join("; ", errors.Take(3)) : "None";
                
                return (validCandles > 0, errorString, qualityScore, lines.Length, firstDate, lastDate, format);
            }
            catch (Exception ex)
            {
                return (false, $"Validation error: {ex.Message}", 0, 0, DateTime.MinValue, DateTime.MinValue, "Unknown");
            }
        }

        private Candle ParseCandleFromLine(string line, string format)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 6) return null;
                
                DateTime time;
                if (format == "YYYYMMDD HHMMSS")
                {
                    if (parts.Length < 2) return null;
                    string dateStr = parts[0];
                    string timeStr = parts[1];
                    
                    int year = int.Parse(dateStr.Substring(0, 4));
                    int month = int.Parse(dateStr.Substring(4, 2));
                    int day = int.Parse(dateStr.Substring(6, 2));
                    
                    int hour = timeStr.Length >= 2 ? int.Parse(timeStr.Substring(0, 2)) : 0;
                    int minute = timeStr.Length >= 4 ? int.Parse(timeStr.Substring(2, 2)) : 0;
                    int second = timeStr.Length >= 6 ? int.Parse(timeStr.Substring(4, 2)) : 0;
                    
                    time = new DateTime(year, month, day, hour, minute, second);
                }
                else if (format == "UnixTimestamp")
                {
                    long timestamp = long.Parse(parts[0]);
                    time = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                }
                else
                {
                    if (DateTime.TryParse(parts[0], out time))
                    {
                    }
                    else
                    {
                        return null;
                    }
                }
                
                decimal open = decimal.Parse(parts[format == "UnixTimestamp" ? 1 : 2], CultureInfo.InvariantCulture);
                decimal high = decimal.Parse(parts[format == "UnixTimestamp" ? 2 : 3], CultureInfo.InvariantCulture);
                decimal low = decimal.Parse(parts[format == "UnixTimestamp" ? 3 : 4], CultureInfo.InvariantCulture);
                decimal close = decimal.Parse(parts[format == "UnixTimestamp" ? 4 : 5], CultureInfo.InvariantCulture);
                int volume = parts.Length > (format == "UnixTimestamp" ? 5 : 6) ? int.Parse(parts[format == "UnixTimestamp" ? 5 : 6]) : 0;
                
                return new Candle
                {
                    TimeStart = time,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                };
            }
            catch
            {
                return null;
            }
        }

        private string DetectFileFormat(string firstLine)
        {
            if (string.IsNullOrEmpty(firstLine))
                return "Unknown";
            
            var parts = firstLine.Split(',');
            if (parts.Length < 6)
                return "Unknown";
            
            if (parts[0].Length == 8 && int.TryParse(parts[0], out _))
            {
                return "YYYYMMDD HHMMSS";
            }
            
            if (long.TryParse(parts[0], out long timestamp) && timestamp > 631152000)
            {
                return "UnixTimestamp";
            }
            
            if (DateTime.TryParse(parts[0], out _))
            {
                return "DateTime";
            }
            
            return "Unknown";
        }

        private int CalculateQualityScore(int totalLines, int validCandles, DateTime firstDate, DateTime lastDate)
        {
            if (totalLines == 0) return 0;
            
            int score = 0;
            
            double dataQuality = (double)validCandles / totalLines * 100;
            score += (int)(dataQuality * 0.4);
            
            if (totalLines > 10000) score += 30;
            else if (totalLines > 1000) score += 20;
            else if (totalLines > 100) score += 10;
            
            TimeSpan dateRange = lastDate - firstDate;
            if (dateRange.TotalDays > 365) score += 20;
            else if (dateRange.TotalDays > 90) score += 15;
            else if (dateRange.TotalDays > 30) score += 10;
            else if (dateRange.TotalDays > 7) score += 5;
            
            TimeSpan freshness = DateTime.Now - lastDate;
            if (freshness.TotalDays < 1) score += 10;
            else if (freshness.TotalDays < 7) score += 7;
            else if (freshness.TotalDays < 30) score += 3;
            
            return Math.Min(score, 100);
        }

        public DataQualityReport GenerateQualityReport()
        {
            lock (_dataLock)
            {
                var report = new DataQualityReport
                {
                    ReportTime = DateTime.Now,
                    TotalFiles = _dataFiles.Count,
                    ValidFiles = _dataFiles.Count(f => f.IsValid),
                    InvalidFiles = _dataFiles.Count(f => !f.IsValid),
                    TotalSizeBytes = _dataFiles.Sum(f => f.FileSize),
                    TotalCandles = _dataFiles.Sum(f => f.CandleCount),
                    ProblemFiles = _dataFiles.Where(f => !f.IsValid || f.QualityScore < 50).ToList(),
                    AverageQualityScore = _dataFiles.Any() ? _dataFiles.Average(f => f.QualityScore) : 0
                };
                
                if (_dataFiles.Any(f => f.IsValid))
                {
                    report.OldestData = _dataFiles.Where(f => f.IsValid).Min(f => f.FirstCandleDate);
                    report.NewestData = _dataFiles.Where(f => f.IsValid).Max(f => f.LastCandleDate);
                }
                else
                {
                    report.OldestData = DateTime.MinValue;
                    report.NewestData = DateTime.MinValue;
                }
                
                report.SymbolsByTimeframe = new Dictionary<string, int>();
                foreach (var timeframe in _supportedTimeframes)
                {
                    int count = _availableSymbolsByTimeframe[timeframe].Count;
                    if (count > 0)
                    {
                        report.SymbolsByTimeframe[timeframe] = count;
                    }
                }
                
                _lastQualityReport = report;
                return report;
            }
        }

        public string ExportDataQualityReport()
        {
            var report = GenerateQualityReport();
            return _exporter.ExportDataQualityReport(report);
        }

        public string ExportCurrentScannerData()
        {
            return _exporter.ExportCurrentData();
        }

        public List<DataFileInfo> GetAllDataFiles()
        {
            lock (_dataLock)
            {
                return new List<DataFileInfo>(_dataFiles);
            }
        }

        public List<string> GetAllSymbols()
        {
            lock (_dataLock)
            {
                var allSymbols = new HashSet<string>();
                foreach (var timeframe in _availableSymbolsByTimeframe.Values)
                {
                    foreach (var symbol in timeframe)
                    {
                        allSymbols.Add(symbol);
                    }
                }
                return allSymbols.OrderBy(s => s).ToList();
            }
        }

        public List<string> GetSymbolsByTimeframe(string timeframe)
        {
            lock (_dataLock)
            {
                if (_availableSymbolsByTimeframe.ContainsKey(timeframe))
                {
                    return new List<string>(_availableSymbolsByTimeframe[timeframe]);
                }
                return new List<string>();
            }
        }

        public List<string> GetAvailableTimeframes()
        {
            lock (_dataLock)
            {
                return _supportedTimeframes
                    .Where(tf => _availableSymbolsByTimeframe[tf].Count > 0)
                    .ToList();
            }
        }

        public List<string> GetTimeframesForSymbol(string symbol)
        {
            lock (_dataLock)
            {
                var timeframes = new List<string>();
                foreach (var kvp in _availableSymbolsByTimeframe)
                {
                    if (kvp.Value.Contains(symbol))
                    {
                        timeframes.Add(kvp.Key);
                    }
                }
                return timeframes;
            }
        }

        public DataFileInfo GetBestDataFile(string symbol, string preferredTimeframe = null)
        {
            lock (_dataLock)
            {
                var symbolFiles = _dataFiles
                    .Where(f => f.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) && f.IsValid)
                    .ToList();
                
                if (!symbolFiles.Any())
                    return null;
                
                if (!string.IsNullOrEmpty(preferredTimeframe))
                {
                    var preferredFile = symbolFiles
                        .FirstOrDefault(f => f.TimeFrame.Equals(preferredTimeframe, StringComparison.OrdinalIgnoreCase));
                    
                    if (preferredFile != null)
                        return preferredFile;
                }
                
                return symbolFiles.OrderByDescending(f => f.QualityScore).FirstOrDefault();
            }
        }

        public string GetDataFilePath(string symbol, string timeframe = null)
        {
            var dataFile = GetBestDataFile(symbol, timeframe);
            return dataFile?.FilePath;
        }

        private void LogAvailableData()
        {
            lock (_dataLock)
            {
                var report = GenerateQualityReport();
                
                _bot.SendNewLogMessage("📊 ===== ДОСТУПНЫЕ ДАННЫЕ =====", LogMessageType.System);
                _bot.SendNewLogMessage(report.GetSummary(), LogMessageType.System);
                
                foreach (var timeframe in _supportedTimeframes)
                {
                    var symbols = _availableSymbolsByTimeframe[timeframe];
                    if (symbols.Count > 0)
                    {
                        _bot.SendNewLogMessage($"📈 {timeframe}: {symbols.Count} символов", LogMessageType.System);
                        
                        var symbolGroups = symbols
                            .Select((s, i) => new { Symbol = s, Index = i })
                            .GroupBy(x => x.Index / 10)
                            .Select(g => string.Join(", ", g.Select(x => x.Symbol)));
                        
                        foreach (var group in symbolGroups)
                        {
                            _bot.SendNewLogMessage($"   {group}", LogMessageType.System);
                        }
                    }
                }
                
                _bot.SendNewLogMessage("=================================", LogMessageType.System);
            }
        }

        public string GetDataStatusSummary()
        {
            lock (_dataLock)
            {
                var report = GenerateQualityReport();
                return report.GetSummary();
            }
        }

        public Dictionary<string, object> GetDetailedDataMetrics()
        {
            lock (_dataLock)
            {
                var metrics = new Dictionary<string, object>
                {
                    ["TotalFiles"] = _dataFiles.Count,
                    ["ValidFiles"] = _dataFiles.Count(f => f.IsValid),
                    ["TotalSymbols"] = GetAllSymbols().Count,
                    ["TotalTimeframes"] = GetAvailableTimeframes().Count,
                    ["TotalCandles"] = _dataFiles.Sum(f => f.CandleCount),
                    ["TotalSizeMB"] = Math.Round(_dataFiles.Sum(f => f.FileSize) / (1024.0 * 1024.0), 2),
                    ["AverageQuality"] = Math.Round(_dataFiles.Average(f => f.QualityScore), 1),
                    ["LastScan"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                return metrics;
            }
        }

        public bool ImportHistoricalData(string sourcePath, string targetSymbol = null, string targetTimeframe = null)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    _bot.SendNewLogMessage($"❌ Файл не найден: {sourcePath}", LogMessageType.Error);
                    return false;
                }

                string fileName = Path.GetFileName(sourcePath);
                string destDir = Path.Combine(_dataPath, "HistoricalData");
                
                string destFileName;
                if (!string.IsNullOrEmpty(targetSymbol) && !string.IsNullOrEmpty(targetTimeframe))
                {
                    destFileName = $"{targetSymbol}_{targetTimeframe}.txt";
                }
                else if (!string.IsNullOrEmpty(targetSymbol))
                {
                    string detectedTimeframe = DetectTimeframeFromFileName(fileName) ?? "M1";
                    destFileName = $"{targetSymbol}_{detectedTimeframe}.txt";
                }
                else
                {
                    destFileName = fileName;
                }
                
                string destPath = Path.Combine(destDir, destFileName);
                
                File.Copy(sourcePath, destPath, true);
                
                _bot.SendNewLogMessage($"✅ Данные импортированы: {destFileName}", LogMessageType.System);
                
                ScanDataFiles();
                
                return true;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка импорта данных: {ex.Message}", LogMessageType.Error);
                return false;
            }
        }

        public bool ValidateAndRepairDataFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _bot.SendNewLogMessage($"❌ Файл не найден: {filePath}", LogMessageType.Error);
                    return false;
                }

                var lines = File.ReadAllLines(filePath);
                var validLines = new List<string>();
                int repairedCount = 0;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;
                    
                    string repairedLine = TryRepairDataLine(lines[i]);
                    if (!string.IsNullOrEmpty(repairedLine))
                    {
                        validLines.Add(repairedLine);
                        if (repairedLine != lines[i])
                            repairedCount++;
                    }
                }
                
                if (repairedCount > 0)
                {
                    File.WriteAllLines(filePath, validLines);
                    _bot.SendNewLogMessage($"✅ Файл исправлен: {Path.GetFileName(filePath)}. Исправлено строк: {repairedCount}", 
                                          LogMessageType.System);
                }
                else
                {
                    _bot.SendNewLogMessage($"✅ Файл в порядке: {Path.GetFileName(filePath)}", 
                                          LogMessageType.System);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _bot.SendNewLogMessage($"❌ Ошибка проверки файла: {ex.Message}", LogMessageType.Error);
                return false;
            }
        }

        private string TryRepairDataLine(string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 6)
                {
                    while (parts.Length < 6)
                    {
                        line += ",0";
                        parts = line.Split(',');
                    }
                }
                
                for (int i = 2; i < Math.Min(6, parts.Length); i++)
                {
                    if (!decimal.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    {
                        parts[i] = "0";
                    }
                }
                
                return string.Join(",", parts);
            }
            catch
            {
                return null;
            }
        }
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
        private FlexibleInstrumentManager _instrumentManager;
        private AbsoluteLossProtectionComponent _lossProtection;
        
        private TrailingStopLearner _trailingLearner;
        private TrailingStopComponent _trailingComponent;

        private string _dataPath;

        // Для хранения предыдущих значений индикаторов для детекции пересечений
        private decimal _previousTenkan = 0;
        private decimal _previousKijun = 0;
        
        private readonly ConcurrentDictionary<string, InstrumentData> _instrumentData =
            new ConcurrentDictionary<string, InstrumentData>();
        private readonly ConcurrentDictionary<string, DateTime> _activeInstruments =
            new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, DateTime> _lastOrderTimes =
            new ConcurrentDictionary<string, DateTime>();
        private readonly List<BotTabSimple> _subscribedTabs = new List<BotTabSimple>();
        private CancellationTokenSource _continuousOptimizationCts;
        
        // Параметры для гибкой системы
        public StrategyParameterBool EnableAutoDiscovery;
        public StrategyParameterInt AutoScanInterval;
        public StrategyParameterBool EnableDataQualityMonitoring;
        public StrategyParameterInt MinDataQualityScore;
        public StrategyParameterBool EnableBatchOptimization;
        public StrategyParameterInt MaxBatchSymbols;
        public StrategyParameterBool AutoExportResults;

        // Параметры для мульти-инструментальной торговли
        public StrategyParameterBool AutoDetectInstruments;
        public StrategyParameterString SupportedInstruments;
        
        // Существующие параметры
        public StrategyParameterBool PsoAutoOptimize;
        public StrategyParameterInt PsoOptimizationInterval;
        public StrategyParameterInt PsoPopulationSize;
        public StrategyParameterInt PsoMaxIterations;
        public StrategyParameterBool PsoUseEnhancedMetrics;
        public StrategyParameterString AiOptimizationMode;
        public StrategyParameterBool ContinuousOptimization;

        public StrategyParameterString HistoricalDataSymbol;
        public StrategyParameterString HistoricalDataPath;
        public StrategyParameterBool UseHistoricalBacktesting;
        public StrategyParameterInt BacktestPeriodDays;
        
        public StrategyParameterInt IchimokuTenkan;
        public StrategyParameterInt IchimokuKijun;
        public StrategyParameterInt IchimokuSenkouB;
        public StrategyParameterInt RsiPeriod;
        
        public StrategyParameterInt Ema1Period;
        public StrategyParameterInt Ema2Period;
        public StrategyParameterInt Ema3Period;
        
        public StrategyParameterDecimal DistanceBetweenOrders;
        
        public StrategyParameterDecimal TakeProfitLong;
        public StrategyParameterDecimal TakeProfitShort;
        
        public StrategyParameterDecimal MinProfitPercent;
        public StrategyParameterBool UseAbsoluteProtection;
        public StrategyParameterDecimal BreakevenTriggerPercent;

        public StrategyParameterString TradingMode;
        public StrategyParameterBool EnableLong;
        public StrategyParameterBool EnableShort;
        public StrategyParameterInt MaxTradingInstruments;
        public StrategyParameterInt MaxBotPositions;
        public StrategyParameterString PositionCloseMode;
        public StrategyParameterBool ForceTrading;

        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterDecimal VolumeReductionPerOrder;
        
        public StrategyParameterBool UseDrawdownProtection;
        public StrategyParameterDecimal MaxDrawdownPerInstrument;
        public StrategyParameterDecimal VolumeReductionFactor;
        
        public StrategyParameterBool UseDuplicateProtection;
        public StrategyParameterInt DuplicateProtectionMinutes;
        public StrategyParameterDecimal DuplicatePriceTolerancePercent;
        
        public StrategyParameterBool UseTrendFilter;
        public StrategyParameterBool UseRsiFilter;
        public StrategyParameterDecimal RsiOverbought;
        public StrategyParameterDecimal RsiOversold;
        public StrategyParameterBool UseIchimokuFilter;
        
        public StrategyParameterBool UseTradeDelay;
        public StrategyParameterInt DelayBetweenOrdersSeconds;
        public StrategyParameterBool UnrealizedPnLMonitoring;
        public StrategyParameterDecimal MaxUnrealizedLossPerInstrument;

        public StrategyParameterBool EnableGlobalLearning;
        public StrategyParameterDecimal SelfLearn_SuccessMultiplier;
        public StrategyParameterDecimal SelfLearn_FailureMultiplier;
        public StrategyParameterDecimal SelfLearn_DayBonus;
        public StrategyParameterDecimal SelfLearn_DayPenalty;

        public PSOScreenerHybridPro(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            Description = "Профессиональный гибридный скринер: PSO+GA AI оптимизация с абсолютной защитой от убытков";

            _dataPath = GetDefaultDataPath();
            
            CreateParameters();
            InitializeParameters(); // Добавлен вызов инициализации параметров
            InitializeComponentArchitecture();
            InitializeEventSubscriptions();
            
            SendNewLogMessage($"🤖 Профессиональный PSO+GA скринер инициализирован. Путь данных: {_dataPath}", 
                            LogMessageType.System);
        }

        private string GetDefaultDataPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string dataPath = Path.Combine(basePath, "Data", "AI_Optimization");
            
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            
            string[] subfolders = { "HistoricalData", "OptimizationResults", "TrailingLearning", 
                                   "BacktestResults", "Exports" };
            foreach (var folder in subfolders)
            {
                string fullPath = Path.Combine(dataPath, folder);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
            }
            
            return dataPath;
        }

        private void InitializeComponentArchitecture()
        {
            _assembly = new ComponentAssembly();
            _stateMachine = new AdaptiveTradingStateMachine(this);
            
            _trailingLearner = new TrailingStopLearner(this, _dataPath);
            _trailingComponent = new TrailingStopComponent(this, _trailingLearner);
            _positionManager = new PositionManager();
            _lossProtection = new AbsoluteLossProtectionComponent(this, _positionManager);
            _aiEngine = new HybridAiOptimizationEngine(this, _dataPath);
            _instrumentManager = new FlexibleInstrumentManager(this, _dataPath);
            
            _assembly.RegisterComponent(_trailingLearner);
            _assembly.RegisterComponent(_trailingComponent);
            _assembly.RegisterComponent(_positionManager);
            _assembly.RegisterComponent(_lossProtection);
            _assembly.RegisterComponent(_aiEngine);
            _assembly.RegisterComponent(_instrumentManager);
            
            _assembly.Initialize(this);
            _stateMachine.TransitionTo(AdaptiveTradingStateMachine.TradingState.WaitingForSignals);
            
            if (EnableGlobalLearning != null)
            {
                _trailingComponent.SetGlobalLearning(EnableGlobalLearning.ValueBool);
            }
        }

        private void CreateParameters()
        {
            // Новые параметры для гибкой системы
            EnableAutoDiscovery = CreateParameter("🔍 Автообнаружение данных", true);
            AutoScanInterval = CreateParameter("🔄 Интервал сканирования (мин)", 5, 1, 60, 1);
            EnableDataQualityMonitoring = CreateParameter("📊 Мониторинг качества данных", true);
            MinDataQualityScore = CreateParameter("⭐ Мин. качество данных", 70, 0, 100, 5);
            EnableBatchOptimization = CreateParameter("📦 Пакетная оптимизация", false);
            MaxBatchSymbols = CreateParameter("📈 Макс. символов в пакете", 10, 1, 50, 1);
            AutoExportResults = CreateParameter("💾 Автоэкспорт результатов", true);
            
            // Существующие параметры оптимизации
            HistoricalDataSymbol = CreateParameter("📊 Основной символ для оптимизации", "AUTO",
                new[] { "AUTO" });
            HistoricalDataPath = CreateParameter("📁 Путь к историческим данным", _dataPath);
            UseHistoricalBacktesting = CreateParameter("📈 Использовать исторический бэктестинг", true);
            BacktestPeriodDays = CreateParameter("📅 Период бэктестинга (дней)", 365, 30, 1095, 30);

            // Дополнительные параметры для мульти-инструментальной торговли
            AutoDetectInstruments = CreateParameter("🔍 Автоопределение инструментов из скринера", true);
            SupportedInstruments = CreateParameter("📋 Поддерживаемые инструменты (через запятую)", "");
            
            AiOptimizationMode = CreateParameter("🤖 AI Оптимизация", "Гибридная", 
                new[] { "Выключена", "PSO", "GA", "Гибридная", "Авто" });
            ContinuousOptimization = CreateParameter("🔄 Непрерывная оптимизация", true);
            PsoAutoOptimize = CreateParameter("Автооптимизация PSO", true);
            PsoOptimizationInterval = CreateParameter("Интервал оптимизации (мин)", 120, 60, 480, 60);
            PsoPopulationSize = CreateParameter("PSO: Размер роя", 50, 20, 200, 10);
            PsoMaxIterations = CreateParameter("PSO: Макс. итераций", 100, 50, 500, 50);
            PsoUseEnhancedMetrics = CreateParameter("Расширенные метрики PSO", true);

            EnableGlobalLearning = CreateParameter("Включить глобальное обучение трейлинга", false);
            SelfLearn_SuccessMultiplier = CreateParameter("SelfLearn: Success Multiplier", 1.2m, 1.0m, 2.0m, 0.1m);
            SelfLearn_FailureMultiplier = CreateParameter("SelfLearn: Failure Multiplier", 0.8m, 0.5m, 1.0m, 0.1m);
            SelfLearn_DayBonus = CreateParameter("SelfLearn: Day Bonus", 1.1m, 1.0m, 2.0m, 0.1m);
            SelfLearn_DayPenalty = CreateParameter("SelfLearn: Day Penalty", 0.9m, 0.5m, 1.0m, 0.1m);

            // Ключевые параметры согласно инструкции
            MinProfitPercent = CreateParameter("Минимальная прибыль %", 0.45m, 0.1m, 2.0m, 0.05m);
            UseAbsoluteProtection = CreateParameter("Абсолютная защита", true);
            BreakevenTriggerPercent = CreateParameter("Триггер безубытка %", 0.40m, 0.1m, 1.0m, 0.05m);

            DistanceBetweenOrders = CreateParameter("Расстояние между ордерами %", 0.3m, 0.1m, 1.0m, 0.1m);
            TakeProfitLong = CreateParameter("Тейк-профит Лонг %", 0.5m, 0.2m, 1.5m, 0.1m);
            TakeProfitShort = CreateParameter("Тейк-профит Шорт %", 0.3m, 0.1m, 1.0m, 0.1m);
            
            // Параметры Ichimoku с явными значениями по умолчанию
            IchimokuTenkan = CreateParameter("Ichimoku Tenkan Period", 9, 5, 20, 1);
            IchimokuKijun = CreateParameter("Ichimoku Kijun Period", 26, 20, 30, 1);
            IchimokuSenkouB = CreateParameter("Ichimoku Senkou B Period", 52, 40, 60, 1);
            RsiPeriod = CreateParameter("RSI Period", 14, 10, 20, 1);
            
            TradingMode = CreateParameter("Режим торговли", "On", new[] { "On", "Off", "Only Close Position" });
            PositionCloseMode = CreateParameter("Режим закрытия", "Общая позиция", 
                new[] { "Общая позиция", "По отдельным сделкам" });
            EnableLong = CreateParameter("Включить Лонг", true);
            EnableShort = CreateParameter("Включить Шорт", false);
            MaxTradingInstruments = CreateParameter("Макс. инструментов", 5, 1, 10, 1);
            MaxBotPositions = CreateParameter("Макс. позиций бота", 10, 1, 50, 1);
            ForceTrading = CreateParameter("Принудительная торговля", false);

            VolumeType = CreateParameter("Тип объема", "Contracts", 
                new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Базовый объем", 1m, 0.1m, 5m, 0.1m);
            TradeAssetInPortfolio = CreateParameter("Базовый актив портфеля", "Prime", 
                new[] { "Prime", "RUB", "USD", "EUR" });
            VolumeReductionPerOrder = CreateParameter("Уменьшение объема %", 5m, 0m, 20m, 1m);

            UseDrawdownProtection = CreateParameter("Защита от просадки", true);
            MaxDrawdownPerInstrument = CreateParameter("Макс. просадка инструмента %", 3m, 1m, 10m, 0.5m);
            VolumeReductionFactor = CreateParameter("Коэф. снижения объема", 0.5m, 0.2m, 0.8m, 0.1m);
            
            UseDuplicateProtection = CreateParameter("Защита от дублей", true);
            DuplicateProtectionMinutes = CreateParameter("Время защиты от дублей (мин)", 5, 1, 30, 1);
            DuplicatePriceTolerancePercent = CreateParameter("Допуск цены для дублей %", 0.1m, 0.01m, 1.0m, 0.01m);

            UseTrendFilter = CreateParameter("Фильтр тренда", true);
            UseRsiFilter = CreateParameter("Фильтр RSI", true);
            RsiOverbought = CreateParameter("RSI перекупленность", 70m, 60m, 80m, 2m);
            RsiOversold = CreateParameter("RSI перепроданность", 30m, 20m, 40m, 2m);
            UseIchimokuFilter = CreateParameter("Фильтр Ишимоку", true);

            UseTradeDelay = CreateParameter("Использовать задержку", true);
            DelayBetweenOrdersSeconds = CreateParameter("Задержка между ордерами (сек)", 2, 0, 15, 1);
            UnrealizedPnLMonitoring = CreateParameter("Мониторинг нереал. PnL", true);
            MaxUnrealizedLossPerInstrument = CreateParameter("Макс. нереал. убыток на инструмент %", 3m, 1m, 10m, 0.5m);
        }

        private void InitializeParameters()
        {
            try
            {
                // Проверяем и инициализируем параметры Ichimoku
                var (tenkan, kijun, senkouB, rsi) = GetSafeIchimokuParameters();
                
                SendNewLogMessage($"📊 Параметры Ichimoku инициализированы: " +
                                $"Tenkan={tenkan}, Kijun={kijun}, SenkouB={senkouB}, RSI={rsi}",
                                LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка инициализации параметров: {ex.Message}", LogMessageType.Error);
            }
        }

        private (int tenkan, int kijun, int senkouB, int rsi) GetSafeIchimokuParameters()
        {
            try
            {
                int tenkan = IchimokuTenkan?.ValueInt ?? 9;
                int kijun = IchimokuKijun?.ValueInt ?? 26;
                int senkouB = IchimokuSenkouB?.ValueInt ?? 52;
                int rsi = RsiPeriod?.ValueInt ?? 14;
                
                // Логирование для отладки
                if (IchimokuTenkan == null || IchimokuKijun == null || IchimokuSenkouB == null || RsiPeriod == null)
                {
                    SendNewLogMessage($"⚠️ Параметры Ichimoku не инициализированы, используются значения по умолчанию: " +
                                    $"Tenkan={tenkan}, Kijun={kijun}, SenkouB={senkouB}, RSI={rsi}",
                                    LogMessageType.System);
                }
                
                return (tenkan, kijun, senkouB, rsi);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка получения параметров Ichimoku: {ex.Message}. Используются значения по умолчанию.",
                                LogMessageType.Error);
                return (9, 26, 52, 14);
            }
        }

        private void InitializeEventSubscriptions()
        {
            if (TabsScreener != null && TabsScreener.Count > 0)
            {
                var screener = TabsScreener[0];
                screener.CandleFinishedEvent += TabScreener_CandleFinishedEvent;

                // ПРОБЛЕМА: Подписка только на существующие вкладки.
                // Новые вкладки, добавляемые динамически, не будут иметь подписки.
                // РЕШЕНИЕ: Добавить таймер для периодической проверки новых вкладок
                // или подписаться на событие добавления вкладок (если доступно).

                // Временное решение: Периодическая проверка новых вкладок
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            SubscribeToNewTabs(screener);
                            await Task.Delay(TimeSpan.FromSeconds(30)); // Проверка каждые 30 секунд
                        }
                        catch (Exception ex)
                        {
                            SendNewLogMessage($"❌ Ошибка проверки новых вкладок: {ex.Message}", LogMessageType.Error);
                            await Task.Delay(TimeSpan.FromSeconds(60));
                        }
                    }
                });

                SubscribeToExistingTabs(screener);
            }
            else
            {
                SendNewLogMessage("❌ Ошибка: TabScreener не инициализирован", LogMessageType.Error);
            }
        }

        private void SubscribeToExistingTabs(BotTabScreener screener)
        {
            foreach (var tab in screener.Tabs)
            {
                if (tab is BotTabSimple simpleTab)
                {
                    simpleTab.PositionOpeningSuccesEvent += Position_OpeningSuccesEvent;
                    simpleTab.PositionClosingSuccesEvent += Position_ClosingSuccesEvent;

                    // Подхват ручных позиций
                    simpleTab.PositionOpeningSuccesEvent += (position) =>
                    {
                        if (position != null && !_positionManager.IsBotPosition(position))
                        {
                            _positionManager.CaptureManualPosition(position);
                        }
                    };
                }
            }

            SendNewLogMessage($"✅ Подписка на события {screener.Tabs.Count} существующих вкладок", LogMessageType.System);
        }

        private void SubscribeToNewTabs(BotTabScreener screener)
        {
            int subscribedCount = 0;

            foreach (var tab in screener.Tabs)
            {
                if (tab is BotTabSimple simpleTab)
                {
                    // Проверяем, не подписаны ли уже на события этой вкладки
                    // (предотвращаем дублирование подписок)
                    bool alreadySubscribed = _subscribedTabs.Contains(simpleTab);

                    if (!alreadySubscribed)
                    {
                        simpleTab.PositionOpeningSuccesEvent += Position_OpeningSuccesEvent;
                        simpleTab.PositionClosingSuccesEvent += Position_ClosingSuccesEvent;

                        // Подхват ручных позиций
                        simpleTab.PositionOpeningSuccesEvent += (position) =>
                        {
                            if (position != null && !_positionManager.IsBotPosition(position))
                            {
                                _positionManager.CaptureManualPosition(position);
                            }
                        };

                        _subscribedTabs.Add(simpleTab);
                        subscribedCount++;
                    }
                }
            }

            if (subscribedCount > 0)
            {
                SendNewLogMessage($"✅ Подписка на события {subscribedCount} новых вкладок", LogMessageType.System);
            }
        }

        #region ОСНОВНЫЕ МЕТОДЫ ОТКРЫТИЯ СДЕЛОК
        
        /// <summary>
        /// Проверка условий и открытие LONG сделки
        /// </summary>
        private void CheckLongConditions(string security, BotTabSimple tab, Candle currentCandle, EnhancedTrendAnalysis trend)
        {
            // Проверка фильтров
            if (!PassFilters(trend, "Long")) return;

            // Проверка задержки
            if (!CanOpenOrder(security, "Long")) return;

            // Проверка лимита позиций
            if (!CanOpenNewBotPosition()) return;

            decimal currentPrice = currentCandle.Close;
            
            // Проверка расстояния между ордерами
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
                        
                        // ОТКРЫТИЕ СДЕЛКИ
                        tab.BuyAtMarket(volume);
                        UpdateLastOrderTime(security, "Long");
                        
                        LogTradeOpened(security, "LONG", currentPrice, volume, trend);
                    }
                }
            }
        }

        /// <summary>
        /// Проверка условий и открытие SHORT сделки
        /// </summary>
        private void CheckShortConditions(string security, BotTabSimple tab, Candle currentCandle, EnhancedTrendAnalysis trend)
        {
            // Проверка фильтров
            if (!PassFilters(trend, "Short")) return;

            // Проверка задержки
            if (!CanOpenOrder(security, "Short")) return;

            // Проверка лимита позиций
            if (!CanOpenNewBotPosition()) return;

            decimal currentPrice = currentCandle.Close;
            
            // Проверка расстояния между ордерами
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
                        
                        // ОТКРЫТИЕ СДЕЛКИ
                        tab.SellAtMarket(volume);
                        UpdateLastOrderTime(security, "Short");
                        
                        LogTradeOpened(security, "SHORT", currentPrice, volume, trend);
                    }
                }
            }
        }

        #endregion

        #region ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ОТКРЫТИЯ

        /// <summary>
        /// Проверяет, нужно ли открывать следующую сделку
        /// Основа стратегии усреднения - открытие по мере движения цены
        /// </summary>
        private bool ShouldOpenNextOrder(string security, BotTabSimple tab, decimal currentPrice, string direction)
        {
            try
            {
                var positions = tab.PositionsOpenAll
                    .Where(p => p.State == PositionStateType.Open && 
                           p.Direction == (direction == "Long" ? Side.Buy : Side.Sell))
                    .ToList();

                // Если позиций нет - открываем первую сразу
                if (!positions.Any()) return true;

                // Берем последнюю открытую позицию
                var lastPosition = positions.OrderByDescending(p => p.TimeOpen).First();
                decimal requiredDistance = DistanceBetweenOrders.ValueDecimal / 100m;
                
                // Для LONG: ждем пока цена УПАДЕТ на заданное расстояние
                // Для SHORT: ждем пока цена ВЫРАСТЕТ на заданное расстояние
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

        /// <summary>
        /// Проверяет, есть ли уже позиция на цене близкой к текущей
        /// Защита от дублирования сделок на одной цене
        /// </summary>
        private bool HasPositionNearPrice(BotTabSimple tab, decimal currentPrice)
        {
            var positions = tab.PositionsOpenAll;
            
            foreach (var position in positions)
            {
                if (position.State != PositionStateType.Open) continue;
                    
                decimal priceDiff = Math.Abs(position.EntryPrice - currentPrice);
                decimal diffPercent = priceDiff / position.EntryPrice * 100;
                
                // Если разница меньше 0.1% - считаем что позиция уже есть
                if (diffPercent < 0.1m)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Проверяет, можно ли открыть ордер с учетом задержки
        /// </summary>
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

        /// <summary>
        /// Обновляет время последнего ордера
        /// </summary>
        private void UpdateLastOrderTime(string security, string orderType)
        {
            string key = $"{security}_{orderType}";
            _lastOrderTimes[key] = DateTime.Now;
        }

        /// <summary>
        /// Валидация условий открытия ордера
        /// </summary>
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

        /// <summary>
        /// Проверяет лимит на максимальное количество позиций бота
        /// </summary>
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

        #endregion

        #region МЕТОДЫ РАСЧЕТА ОБЪЕМА

        /// <summary>
        /// Расчет объема для сделки
        /// </summary>
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

        #region ФИЛЬТРЫ И СИГНАЛЫ

        /// <summary>
        /// Проверка фильтров для сделки
        /// </summary>
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

        /// <summary>
        /// Сигнал на покупку
        /// </summary>
        private bool GetEnhancedBuySignal(EnhancedTrendAnalysis analysis)
        {
            return analysis.TenkanAboveKijun && 
                   analysis.PriceAboveCloud && 
                   analysis.CloudBullish && 
                   analysis.Rsi < RsiOverbought.ValueDecimal;
        }

        /// <summary>
        /// Сигнал на продажу
        /// </summary>
        private bool GetEnhancedSellSignal(EnhancedTrendAnalysis analysis)
        {
            return !analysis.TenkanAboveKijun && 
                   analysis.PriceBelowCloud && 
                   analysis.CloudBearish && 
                   analysis.Rsi > RsiOversold.ValueDecimal;
        }

        #endregion

        #region ПОМОЩНИКИ

        /// <summary>
        /// Считает количество позиций бота
        /// </summary>
        private int CountBotPositions()
        {
            return _positionManager?.GetBotPositionsCount() ?? 0;
        }

        /// <summary>
        /// Логирование открытия сделки
        /// </summary>
        private void LogTradeOpened(string security, string direction, decimal price, decimal volume, EnhancedTrendAnalysis trend)
        {
            string logMessage = $"🎯 ОТКРЫТА {direction} СДЕЛКА:\n" +
                               $"Инструмент: {security}\n" +
                               $"Цена: {price:F4}\n" +
                               $"Объем: {volume:F2}\n" +
                               $"Тренд: {trend.CurrentTrend}\n" +
                               $"Сила тренда: {trend.TrendStrength:F4}\n" +
                               $"RSI: {trend.RSI:F1}";
            
            SendNewLogMessage(logMessage, LogMessageType.Trade);
        }

        #endregion

        #region EVENT HANDLERS
        private void TabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (candles == null || candles.Count == 0 || tab?.Security == null)
            {
                SendNewLogMessage("⚠️ Пустые данные в событии свечи", LogMessageType.System);
                return;
            }

            string security = "";
            try
            {
                security = tab.Security.Name ?? "Unknown";
            }
            catch
            {
                security = "Unknown";
            }

            try
            {
                // ДОБАВЬТЕ ЛОГИРОВАНИЕ ДЛЯ ОТЛАДКИ
                // ✅ ИСПРАВЛЕНИЕ: Безопасный доступ к Last()
                string lastPriceStr = candles.Count > 0 ? candles[candles.Count - 1].Close.ToString("F4") : "N/A";
                SendNewLogMessage($"📊 Обработка свечи для {security}. Свечей: {candles.Count}, Цена: {lastPriceStr}",
                                LogMessageType.System);

                // ✅ ДОПОЛНИТЕЛЬНАЯ ЗАЩИТА: Проверяем что есть хотя бы одна свеча
                if (candles.Count == 0)
                {
                    SendNewLogMessage($"⚠️ Пустой массив свечей для {security}, пропускаем обработку",
                                    LogMessageType.System);
                    return;
                }

                UpdateInstrumentData(security, tab, candles);
                _assembly.Update();

                if (!CanTradeInstrument(security))
                {
                    SendNewLogMessage($"⛔ Инструмент {security} не доступен для торговли",
                                    LogMessageType.System);
                    return;
                }

                // ✅ ИСПРАВЛЕНИЕ: Безопасный доступ к Last()
                if (candles.Count == 0) return;
                var currentCandle = candles[candles.Count - 1];

                // ОБЯЗАТЕЛЬНО ВЫЗЫВАЙТЕ UpdateTrendAnalysis ПЕРЕД ПРОВЕРКОЙ СИГНАЛОВ
                UpdateTrendAnalysis(security, tab, candles);
                MonitorUnrealizedPnL(security, tab, currentCandle);

                // Проверка режима торговли
                if (TradingMode != null && TradingMode.ValueString == "On")
                {
                    CheckTradingConditions(security, tab, currentCandle);
                }
                else if (ForceTrading != null && ForceTrading.ValueBool)
                {
                    CheckTradingConditions(security, tab, currentCandle);
                }

                CheckExitConditions(security, tab, currentCandle);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка в TabScreener_CandleFinishedEvent: {ex.Message}",
                                LogMessageType.Error);
            }
        }

        private void Position_OpeningSuccesEvent(Position position)
        {
            try
            {
                var positionType = DeterminePositionType(position);
                _positionManager.RegisterPosition(position, positionType);
                LogPositionOpened(position, positionType);
                
                if (positionType == PositionType.Manual)
                {
                    SendNewLogMessage(
                        $"🔄 РУЧНАЯ ПОЗИЦИЯ #{position.Number} ПОДХВАЧЕНА ДЛЯ СОПРОВОЖДЕНИЯ\n" +
                        $"Инструмент: {position.SecurityName}\n" +
                        $"Направление: {position.Direction}\n" +
                        $"Цена входа: {position.EntryPrice:F4}\n" +
                        $"Объем: {Math.Abs(position.OpenVolume):F2}\n" +
                        $"Текущая прибыль: 0.00\n" +
                        $"Режим сопровождения: {PositionCloseMode.ValueString}",
                        LogMessageType.System);
                }
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
                ProcessPositionForLearning(position);
                
                LogDetailedPositionStatistics();
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обработки закрытия позиции: {ex.Message}", 
                                LogMessageType.Error);
            }
        }
        #endregion

        #region ОСНОВНЫЕ МЕТОДЫ
        private void UpdateInstrumentData(string security, BotTabSimple tab, List<Candle> candles)
        {
            try
            {
                if (!_instrumentData.ContainsKey(security))
                {
                    _instrumentData[security] = new InstrumentData
                    {
                        Security = security,
                        HistoricalData = new List<Candle>(),
                        Trend = new EnhancedTrendAnalysis(this)
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

        private void UpdateTrendAnalysis(string security, BotTabSimple tab, List<Candle> candles)
        {
            try
            {
                // Получаем безопасные параметры
                var (tenkanPeriod, kijunPeriod, senkouBPeriod, rsiPeriod) = GetSafeIchimokuParameters();
                
                if (UseRsiFilter != null && UseRsiFilter.ValueBool &&
                    RsiOverbought != null && RsiOversold != null)
                {
                    try
                    {
                        decimal rsi = GetRSIValue(tab, rsiPeriod);

                        if (rsi > RsiOverbought.ValueDecimal)
                        {
                            SendNewLogMessage($"⚠️ {security}: RSI перекупленность ({rsi:F1} > {RsiOverbought.ValueDecimal})",
                                            LogMessageType.System);
                        }
                        else if (rsi < RsiOversold.ValueDecimal)
                        {
                            SendNewLogMessage($"⚠️ {security}: RSI перепроданность ({rsi:F1} < {RsiOversold.ValueDecimal})",
                                            LogMessageType.System);
                        }
                    }
                    catch (Exception rsiEx)
                    {
                        SendNewLogMessage($"❌ Ошибка расчета RSI для {security}: {rsiEx.Message}",
                                        LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка анализа тренда {security}: {ex.Message}",
                                LogMessageType.Error);
            }
        }

        private decimal GetRSIValue(BotTabSimple tab, int period)
        {
            try
            {
                if (tab.CandlesAll == null || tab.CandlesAll.Count < period + 1)
                {
                    SendNewLogMessage($"⚠️ Недостаточно свечей для RSI: {tab.CandlesAll?.Count ?? 0} < {period + 1}",
                                    LogMessageType.System);
                    return 0;
                }

                // Создаем индикатор RSI
                var rsiIndicator = IndicatorsFactory.CreateIndicatorByName("RSI", period.ToString(), false);

                if (rsiIndicator == null)
                {
                    SendNewLogMessage("⚠️ Индикатор RSI недоступен, используем значение по умолчанию 50", LogMessageType.System);
                    return 50; // Нейтральное значение вместо 0
                }

                // Добавляем данные в индикатор
                rsiIndicator.Process(tab.CandlesAll);

                if (rsiIndicator.DataSeries != null && rsiIndicator.DataSeries.Count > 0)
                {
                    var series = rsiIndicator.DataSeries[0];
                    if (series != null && series.Values != null && series.Values.Count > 0)
                    {
                        decimal value = series.Values.Last();
                        SendNewLogMessage($"📊 RSI значение: {value:F2}", LogMessageType.System);
                        return value;
                    }
                }

                SendNewLogMessage("⚠️ RSI индикатор вернул пустые данные", LogMessageType.System);
                return 0;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка расчета RSI: {ex.Message}", LogMessageType.Error);
                return 0;
            }
        }

        private (decimal tenkan, decimal kijun, decimal senkouA, decimal senkouB, decimal chikou) GetIchimokuValues(BotTabSimple tab)
        {
            try
            {
                // Получаем безопасные параметры
                var (tenkanPeriod, kijunPeriod, senkouBPeriod, _) = GetSafeIchimokuParameters();
                
                // Дополнительная проверка на null значения
                if (tab == null || tab.CandlesAll == null || tab.CandlesAll.Count < Math.Max(kijunPeriod, senkouBPeriod))
                {
                    SendNewLogMessage($"⚠️ Недостаточно данных для расчета Ichimoku: " +
                                    $"нужно {Math.Max(kijunPeriod, senkouBPeriod)} свечей, есть {tab?.CandlesAll?.Count ?? 0}",
                                    LogMessageType.System);
                    return (0, 0, 0, 0, 0);
                }

                // Проверка корректности периодов
                if (tenkanPeriod <= 0 || kijunPeriod <= 0 || senkouBPeriod <= 0)
                {
                    SendNewLogMessage($"❌ Некорректные периоды Ichimoku: Tenkan={tenkanPeriod}, Kijun={kijunPeriod}, SenkouB={senkouBPeriod}", LogMessageType.Error);
                    return (0, 0, 0, 0, 0);
                }

                // Пробуем разные варианты названия индикатора Ichimoku
                string[] ichimokuNames = { "Ichimoku", "IchimokuCloud", "Ichimoku Kinko Hyo" };
                var ichimokuIndicator = IndicatorsFactory.CreateIndicatorByName("Ichimoku",
                    $"{tenkanPeriod},{kijunPeriod},{senkouBPeriod}", false);

                foreach (var name in ichimokuNames)
                {
                    try
                    {
                        ichimokuIndicator = IndicatorsFactory.CreateIndicatorByName(name,
                            $"{tenkanPeriod},{kijunPeriod},{senkouBPeriod}", false);
                        if (ichimokuIndicator != null)
                        {
                            SendNewLogMessage($"✅ Индикатор Ichimoku найден под именем: {name}", LogMessageType.System);
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (ichimokuIndicator == null)
                {
                    // Если Ichimoku недоступен, используем альтернативную логику на основе EMA
                    SendNewLogMessage("⚠️ Индикатор Ichimoku недоступен, используем альтернативную логику EMA", LogMessageType.System);
                    return GetAlternativeIchimokuValues(tab);
                }

                if (ichimokuIndicator.DataSeries != null && ichimokuIndicator.DataSeries.Count >= 5)
                {
                    var tenkanSeries = ichimokuIndicator.DataSeries[0]; // Tenkan-sen
                    var kijunSeries = ichimokuIndicator.DataSeries[1];  // Kijun-sen
                    var senkouASeries = ichimokuIndicator.DataSeries[2]; // Senkou Span A
                    var senkouBSeries = ichimokuIndicator.DataSeries[3]; // Senkou Span B
                    var chikouSeries = ichimokuIndicator.DataSeries[4];  // Chikou Span

                    decimal tenkan = (tenkanSeries?.Values != null && tenkanSeries.Values.Count > 0) ? tenkanSeries.Values.Last() : 0;
                    decimal kijun = (kijunSeries?.Values != null && kijunSeries.Values.Count > 0) ? kijunSeries.Values.Last() : 0;
                    decimal senkouA = (senkouASeries?.Values != null && senkouASeries.Values.Count > 0) ? senkouASeries.Values.Last() : 0;
                    decimal senkouB = (senkouBSeries?.Values != null && senkouBSeries.Values.Count > 0) ? senkouBSeries.Values.Last() : 0;
                    decimal chikou = (chikouSeries?.Values != null && chikouSeries.Values.Count > 0) ? chikouSeries.Values.Last() : 0;

                    if (tenkan != 0 && kijun != 0)
                    {
                        // Сохраняем предыдущие значения для детекции пересечений
                        _previousTenkan = tenkan;
                        _previousKijun = kijun;

                        SendNewLogMessage($"📊 Ichimoku значения: Tenkan={tenkan:F4}, Kijun={kijun:F4}, Chikou={chikou:F4}", LogMessageType.System);
                        return (tenkan, kijun, senkouA, senkouB, chikou);
                    }
                }

                // Если индикатор вернул нули, используем альтернативную логику
                SendNewLogMessage("⚠️ Ichimoku вернул нулевые значения, используем альтернативную логику", LogMessageType.System);
                return GetAlternativeIchimokuValues(tab);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка получения значений Ichimoku: {ex.Message}", LogMessageType.Error);
                return GetAlternativeIchimokuValues(tab);
            }
        }

        private (decimal tenkan, decimal kijun, decimal senkouA, decimal senkouB, decimal chikou) GetAlternativeIchimokuValues(BotTabSimple tab)
        {
            try
            {
                // Получаем безопасные параметры
                var (tenkanPeriod, kijunPeriod, senkouBPeriod, _) = GetSafeIchimokuParameters();
                
                if (tab.CandlesAll == null || tab.CandlesAll.Count < Math.Max(tenkanPeriod, kijunPeriod))
                    return (0, 0, 0, 0, 0);

                var candles = tab.CandlesAll.Where(c => c != null).ToList();
                if (candles.Count < kijunPeriod)
                    return (0, 0, 0, 0, 0);

                // Расчет Tenkan-sen (Conversion Line) = (High + Low) / 2 за период tenkanPeriod
                if (candles.Count < tenkanPeriod)
                    return (0, 0, 0, 0, 0);

                var tenkanCandles = candles.Skip(candles.Count - tenkanPeriod).Where(c => c != null);
                if (!tenkanCandles.Any())
                    return (0, 0, 0, 0, 0);
                decimal tenkanHigh = tenkanCandles.Max(c => c.High);
                decimal tenkanLow = tenkanCandles.Min(c => c.Low);
                decimal tenkan = (tenkanHigh + tenkanLow) / 2;

                // Расчет Kijun-sen (Base Line) = (High + Low) / 2 за период kijunPeriod
                var kijunCandles = candles.Skip(candles.Count - kijunPeriod).Where(c => c != null);
                if (!kijunCandles.Any())
                    return (0, 0, 0, 0, 0);
                decimal kijunHigh = kijunCandles.Max(c => c.High);
                decimal kijunLow = kijunCandles.Min(c => c.Low);
                decimal kijun = (kijunHigh + kijunLow) / 2;

                // Senkou Span A = (Tenkan + Kijun) / 2, сдвинутый вперед на kijunPeriod
                decimal senkouA = (tenkan + kijun) / 2;

                // Senkou Span B = (High + Low) / 2 за период senkouBPeriod, сдвинутый вперед на kijunPeriod
                decimal senkouB = 0; // Значение по умолчанию - 0 вместо некорректного kijun
                if (candles.Count >= senkouBPeriod)
                {
                    var senkouBCandles = candles.Skip(candles.Count - senkouBPeriod).Where(c => c != null);
                    if (senkouBCandles.Any())
                    {
                        decimal senkouBHigh = senkouBCandles.Max(c => c.High);
                        decimal senkouBLow = senkouBCandles.Min(c => c.Low);
                        senkouB = (senkouBHigh + senkouBLow) / 2;
                    }
                }
                else if (candles.Count >= kijunPeriod)
                {
                    // Если данных недостаточно для senkouBPeriod, используем данные за kijunPeriod
                    var senkouBCandles = candles.Skip(candles.Count - kijunPeriod).Where(c => c != null);
                    if (senkouBCandles.Any())
                    {
                        decimal senkouBHigh = senkouBCandles.Max(c => c.High);
                        decimal senkouBLow = senkouBCandles.Min(c => c.Low);
                        senkouB = (senkouBHigh + senkouBLow) / 2;
                    }
                }

                // Chikou Span = цена закрытия, сдвинутая назад на kijunPeriod
                decimal chikou = 0;
                if (candles.Count >= kijunPeriod)
                {
                    chikou = candles[candles.Count - kijunPeriod]?.Close ?? 0;
                }

                SendNewLogMessage($"🔄 Альтернативный Ichimoku: Tenkan={tenkan:F4}, Kijun={kijun:F4}, Chikou={chikou:F4}", LogMessageType.System);

                return (tenkan, kijun, senkouA, senkouB, chikou);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка альтернативного расчета Ichimoku: {ex.Message}", LogMessageType.Error);
                return (0, 0, 0, 0, 0);
            }
        }

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

                // Проверяем, является ли инструмент уже активным
                if (_activeInstruments.ContainsKey(security))
                {
                    // Для существующих инструментов проверяем, входит ли он в лимит по времени добавления
                    var orderedActive = _activeInstruments
                        .OrderBy(kv => kv.Value)
                        .Take(MaxTradingInstruments.ValueInt)
                        .Select(kv => kv.Key);

                    bool canTrade = orderedActive.Contains(security);

                    if (!canTrade)
                    {
                        SendNewLogMessage($"⚠️ Инструмент {security} превышает лимит активных инструментов ({MaxTradingInstruments.ValueInt})",
                                        LogMessageType.System);
                    }

                    return canTrade;
                }
                else
                {
                    // Для новых инструментов добавляем только если не превышен лимит
                    if (_activeInstruments.Count < MaxTradingInstruments.ValueInt)
                    {
                        _activeInstruments[security] = DateTime.Now;
                        SendNewLogMessage($"✅ Добавлен инструмент в торговлю: {security} ({_activeInstruments.Count}/{MaxTradingInstruments.ValueInt})",
                                        LogMessageType.System);
                        return true;
                    }
                    else
                    {
                        SendNewLogMessage($"⚠️ Превышен лимит инструментов ({MaxTradingInstruments.ValueInt}). Инструмент {security} пропущен",
                                        LogMessageType.System);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка в CanTradeInstrument: {ex.Message}",
                                LogMessageType.Error);
                return false;
            }
        }

        private void MonitorUnrealizedPnL(string security, BotTabSimple tab, Candle currentCandle)
        {
            if (UnrealizedPnLMonitoring == null || !UnrealizedPnLMonitoring.ValueBool) return;

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

        private decimal CalculatePositionProfit(Position position, BotTabSimple tab, decimal currentPrice)
        {
            try
            {
                if (position == null) return 0;

                if (currentPrice == 0 && tab != null && tab.CandlesAll != null && tab.CandlesAll.Count > 0)
                {
                    currentPrice = tab.CandlesAll.Last().Close;
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

        private decimal GetPortfolioValue(Portfolio portfolio)
        {
            try
            {
                return portfolio?.ValueCurrent ?? 10000m;
            }
            catch
            {
                SendNewLogMessage("❌ Ошибка получения значения портфеля", 
                                LogMessageType.Error);
                return 10000m;
            }
        }

        private void CheckTradingConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            try
            {
                // Логирование параметров для отладки
                var (tenkanParam, kijunParam, senkouBParam, rsiParam) = GetSafeIchimokuParameters();
                SendNewLogMessage($"🔧 Параметры Ichimoku для {security}: " +
                                $"Tenkan={tenkanParam}, Kijun={kijunParam}, SenkouB={senkouBParam}, RSI={rsiParam}",
                                LogMessageType.System);
                
                // ЯВНАЯ ПРОВЕРКА
                if (TradingMode == null)
                {
                    SendNewLogMessage($"❌ TradingMode не инициализирован для {security}",
                                    LogMessageType.Error);
                    return;
                }

                if (TradingMode.ValueString != "On" && !ForceTrading.ValueBool)
                {
                    SendNewLogMessage($"⏸️ Торговля отключена для {security}. Mode: {TradingMode.ValueString}",
                                    LogMessageType.System);
                    return;
                }

                // Проверка основных настроек
                SendNewLogMessage($"🔧 Настройки для {security}: TradingMode={TradingMode?.ValueString}, " +
                                $"EnableLong={EnableLong?.ValueBool}, EnableShort={EnableShort?.ValueBool}, " +
                                $"ForceTrading={ForceTrading?.ValueBool}", LogMessageType.System);

                if (TradingMode.ValueString != "On")
                {
                    if (ForceTrading == null || !ForceTrading.ValueBool)
                    {
                        SendNewLogMessage($"⚠️ Торговля отключена для {security}. TradingMode: {TradingMode.ValueString}", LogMessageType.System);
                        return;
                    }
                }

                if (!CanTradeInstrument(security))
                {
                    SendNewLogMessage($"⚠️ Инструмент {security} не доступен для торговли", LogMessageType.System);
                    return;
                }

                int botPositionsCount = _positionManager?.GetBotPositionsCount() ?? 0;

                if (MaxBotPositions != null && botPositionsCount >= MaxBotPositions.ValueInt)
                {
                    SendNewLogMessage($"⚠️ Достигнут лимит ботовских позиций: {botPositionsCount}/{MaxBotPositions.ValueInt}",
                                    LogMessageType.System);
                    return;
                }

                if (UseTradeDelay != null && UseTradeDelay.ValueBool && DelayBetweenOrdersSeconds != null)
                {
                    if (_lastOrderTimes.ContainsKey(security))
                    {
                        var timeSinceLastOrder = DateTime.Now - _lastOrderTimes[security];
                        if (timeSinceLastOrder.TotalSeconds < DelayBetweenOrdersSeconds.ValueInt)
                        {
                            return;
                        }
                    }
                }

                // Обновление анализа тренда
                if (!_instrumentData.ContainsKey(security))
                {
                    _instrumentData[security] = new InstrumentData
                    {
                        Security = security,
                        HistoricalData = new List<Candle>(),
                        Trend = new EnhancedTrendAnalysis(this)
                    };
                }

                var trend = _instrumentData[security].Trend;
                if (tab.CandlesAll != null && tab.CandlesAll.Count >= 60)
                {
                    var (tenkanPeriod, kijunPeriod, senkouBPeriod, rsiPeriod) = GetSafeIchimokuParameters();

                    // Получаем RSI с помощью платформенного индикатора для consistency
                    decimal currentRsi = 0;
                    try
                    {
                        currentRsi = GetRSIValue(tab, rsiPeriod);
                    }
                    catch
                    {
                        currentRsi = 50; // значение по умолчанию
                    }

                    trend.Update(tab.CandlesAll.Skip(tab.CandlesAll.Count - 60).ToList(),
                        tenkanPeriod, kijunPeriod, senkouBPeriod, rsiPeriod, currentRsi);
                }

                // Проверка условий для открытия позиций
                if (EnableLong != null && EnableLong.ValueBool)
                {
                    CheckLongConditions(security, tab, currentCandle, trend);
                }

                if (EnableShort != null && EnableShort.ValueBool)
                {
                    CheckShortConditions(security, tab, currentCandle, trend);
                }

                // Обновляем предыдущие значения после проверки сигналов
                UpdatePreviousIchimokuValues(tab);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка проверки условий торговли {security}: {ex.Message}",
                                LogMessageType.Error);
            }
        }

        private void UpdatePreviousIchimokuValues(BotTabSimple tab)
        {
            try
            {
                var (tenkan, kijun, _, _, _) = GetIchimokuValues(tab);

                // Проверяем на нулевые значения перед сохранением
                if (tenkan != 0 && kijun != 0)
                {
                    _previousTenkan = tenkan;
                    _previousKijun = kijun;
                    SendNewLogMessage($"📈 Обновлены предыдущие значения: Tenkan={tenkan:F4}, Kijun={kijun:F4}",
                                    LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обновления предыдущих значений Ichimoku: {ex.Message}", LogMessageType.Error);
            }
        }

        private bool CheckLongSignal(BotTabSimple tab, Candle currentCandle)
        {
            try
            {
                // Получаем текущие значения Ишимоку
                var (tenkan, kijun, senkouA, senkouB, chikou) = GetIchimokuValues(tab);

                // Проверяем на нулевые значения
                if (tenkan == 0 || kijun == 0)
                {
                    SendNewLogMessage("⚠️ Ichimoku значения равны 0, пропускаем сигнал", LogMessageType.System);
                    return false;
                }

                // Основной сигнал Ишимоку: Tenkan-sen пересекает Kijun-sen снизу вверх
                bool tenkanCrossesKijunUp = _previousTenkan <= _previousKijun && tenkan > kijun;

                // Проверяем предыдущие значения
                if (_previousTenkan == 0 || _previousKijun == 0)
                {
                    SendNewLogMessage("⚠️ Предыдущие значения Ichimoku равны 0, пропускаем пересечение",
                                    LogMessageType.System);
                    tenkanCrossesKijunUp = false;
                }

                // Дополнительные условия: цена выше облака
                bool priceAboveCloud = currentCandle.Close > Math.Max(senkouA, senkouB);

                // Chikou Span выше цены
                bool chikouAbovePrice = chikou > currentCandle.Close;

                // ФИЛЬТРЫ (опционально)
                bool rsiFilter = true;
                if (UseRsiFilter != null && UseRsiFilter.ValueBool)
                {
                    decimal rsi = GetRSIValue(tab, RsiPeriod.ValueInt);
                    rsiFilter = rsi < RsiOversold.ValueDecimal; // RSI в перепроданной зоне
                }

                SendNewLogMessage($"📊 LONG сигнал Ишимоку: Tenkan={tenkan:F4}, Kijun={kijun:F4}, " +
                                $"PrevTenkan={_previousTenkan:F4}, PrevKijun={_previousKijun:F4}, " +
                                $"CrossUp={tenkanCrossesKijunUp}, PriceAboveCloud={priceAboveCloud}, " +
                                $"ChikouAbove={chikouAbovePrice}, RSIFilter={rsiFilter}",
                                LogMessageType.System);

                // Основной сигнал + фильтры
                return tenkanCrossesKijunUp && priceAboveCloud && chikouAbovePrice && rsiFilter;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка в CheckLongSignal: {ex.Message}", LogMessageType.Error);
                return false;
            }
        }

        private bool IsDuplicateOrder(string security, Side direction, decimal price)
        {
            if (UseDuplicateProtection == null || !UseDuplicateProtection.ValueBool)
                return false;
            
            try
            {
                if (_lastOrderTimes.ContainsKey(security))
                {
                    var timeSinceLastOrder = DateTime.Now - _lastOrderTimes[security];
                    if (timeSinceLastOrder.TotalMinutes < DuplicateProtectionMinutes.ValueInt)
                    {
                        decimal tolerance = price * (DuplicatePriceTolerancePercent.ValueDecimal / 100m);
                        // Проверка на дублирующие ордера
                        return false;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void OpenPosition(string security, BotTabSimple tab, Side direction, Candle currentCandle)
        {
            try
            {
                decimal volume = CalculateVolume(security, tab, direction);
                if (volume <= 0) return;

                decimal entryPrice = currentCandle.Close;

                if (direction == Side.Buy)
                {
                    tab.BuyAtLimit(volume, entryPrice);
                }
                else
                {
                    tab.SellAtLimit(volume, entryPrice);
                }

                _lastOrderTimes[security] = DateTime.Now;

                SendNewLogMessage($"🎯 Открыта позиция {direction} для {security}. Цена: {entryPrice:F4}, Объем: {volume:F2}",
                    LogMessageType.Trade);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка открытия позиции {security}: {ex.Message}",
                    LogMessageType.Error);
            }
        }

        private decimal CalculateVolume(string security, BotTabSimple tab, Side direction)
        {
            try
            {
                if (VolumeType == null) return Volume.ValueDecimal;
                
                switch (VolumeType.ValueString)
                {
                    case "Contracts":
                        return Volume.ValueDecimal;
                    case "Contract currency":
                        return Volume.ValueDecimal;
                    case "Deposit percent":
                        decimal portfolioValue = GetPortfolioValue(tab.Portfolio);
                        return (portfolioValue * Volume.ValueDecimal / 100m);
                    default:
                        return Volume.ValueDecimal;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка расчета объема {security}: {ex.Message}", 
                                LogMessageType.Error);
                return Volume.ValueDecimal;
            }
        }

        private void CheckExitConditions(string security, BotTabSimple tab, Candle currentCandle)
        {
            try
            {
                var positions = tab.PositionsOpenAll;
                decimal currentPrice = currentCandle.Close;

                foreach (var position in positions)
                {
                    if (position.State != PositionStateType.Open) continue;

                    // Проверка абсолютной защиты от убытков
                    if (_lossProtection != null &&
                        !_lossProtection.CanClosePosition(position, tab, currentPrice))
                    {
                        continue;
                    }

                    bool isLong = position.Direction == Side.Buy;
                    decimal takeProfitLevel = CalculateTakeProfit(position);

                    // ИНТЕГРАЦИЯ АДАПТИВНОГО ТРЕЙЛИНГА
                    decimal adaptiveTrailingDistance = 0.3m; // Значение по умолчанию
                    if (_trailingComponent != null)
                    {
                        string dayOfWeek = DateTime.Now.ToString("dddd");
                        adaptiveTrailingDistance = _trailingComponent.CalculateAdaptiveTrailingDistance(0.3m, dayOfWeek);
                    }

                    // Применяем адаптивное расстояние к трейлингу
                    decimal trailingLevel = isLong
                        ? position.EntryPrice * (1 - adaptiveTrailingDistance / 100m)
                        : position.EntryPrice * (1 + adaptiveTrailingDistance / 100m);

                    // Проверяем условия закрытия: тейк-профит ИЛИ трейлинг
                    bool takeProfitHit = isLong ?
                        currentPrice >= takeProfitLevel :
                        currentPrice <= takeProfitLevel;

                    bool trailingHit = isLong ?
                        currentPrice <= trailingLevel :
                        currentPrice >= trailingLevel;

                    if (takeProfitHit || trailingHit || (TradingMode != null && TradingMode.ValueString == "Only Close Position"))
                    {
                        string closeReason = takeProfitHit ? "тейк-профит" :
                                           trailingHit ? $"трейлинг ({adaptiveTrailingDistance:F2}%)" :
                                           "режим закрытия";

                        _stateMachine.TransitionTo(AdaptiveTradingStateMachine.TradingState.PositionClosing);
                        tab.CloseAtMarket(position, position.OpenVolume);
                        LogTradeClosed(security, position, currentPrice, closeReason);
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

        private PositionType DeterminePositionType(Position position)
        {
            return _positionManager?.IsBotPosition(position) == true ? PositionType.Bot : PositionType.Manual;
        }

        private string GetPositionId(Position position)
        {
            return position.Number.ToString();
        }

        private void ProcessPositionForLearning(Position position)
        {
            if (position == null || _trailingLearner == null || EnableGlobalLearning == null || !EnableGlobalLearning.ValueBool) 
                return;

            try
            {
                var positionResult = new PositionResult
                {
                    PositionId = GetPositionId(position),
                    CloseTime = DateTime.Now,
                    PositionType = DeterminePositionType(position),
                    FinalProfitPercent = GetFinalProfitPercent(position),
                    MaxProfitPercent = GetMaxProfitPercent(position),
                    VolatilityAtClose = 1.0m,
                    WasTrailingActive = WasTrailingActive(position)
                };

                _trailingLearner.OnPositionClosed(positionResult);
                
                SendNewLogMessage(
                    $"📊 Обучение трейлинга: Позиция #{GetPositionId(position)} | " +
                    $"Эффективность: {positionResult.TrailingEfficiency:F1}%",
                    LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка обработки позиции для обучения: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        private decimal GetFinalProfitPercent(Position position)
        {
            try
            {
                if (position == null || position.EntryPrice == 0 || position.OpenVolume == 0)
                    return 0;

                decimal priceDiff = position.Direction == Side.Buy 
                    ? position.ClosePrice - position.EntryPrice
                    : position.EntryPrice - position.ClosePrice;
                    
                decimal profit = priceDiff * Math.Abs(position.OpenVolume);
                decimal profitPercent = (profit / (position.EntryPrice * Math.Abs(position.OpenVolume))) * 100m;
                
                return profitPercent;
            }
            catch
            {
                return 0;
            }
        }

        private decimal GetMaxProfitPercent(Position position)
        {
            try
            {
                if (position == null || position.EntryPrice == 0 || position.OpenVolume == 0)
                    return 0;
                    
                return GetFinalProfitPercent(position) * 1.5m;
            }
            catch
            {
                return 0;
            }
        }

        private bool WasTrailingActive(Position position)
        {
            try
            {
                return position.StopOrderIsActive || position.ProfitOrderIsActive;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region ЛОГИРОВАНИЕ С ДЕТАЛЬНОЙ СТАТИСТИКОЙ
        private void LogTradeClosed(string security, Position position, decimal closePrice, string closeReason)
        {
            decimal profit = CalculatePositionProfit(position, null, closePrice);
            decimal profitPercent = position.EntryPrice != 0 ?
                (profit / (position.EntryPrice * Math.Abs(position.OpenVolume))) * 100m : 0;

            var positionStats = _positionManager?.GetPositionStatistics(GetPositionId(position));

            string logMessage = $"✅ ПОЗИЦИЯ ЗАКРЫТА ({closeReason}): {security} | " +
                $"#{GetPositionId(position)} | " +
                $"Тип: {position.Direction} | " +
                $"Цена входа: {position.EntryPrice:F4} | " +
                $"Цена выхода: {closePrice:F4} | " +
                $"Прибыль: {profit:F2} ({profitPercent:F2}%) | " +
                $"Объем: {Math.Abs(position.OpenVolume):F2}";

            if (positionStats != null)
            {
                logMessage += $"\n📊 ДЕТАЛЬНАЯ СТАТИСТИКА:" +
                    $"\n   Макс. прибыль: {positionStats.MaxProfitCurrency:F2} ({positionStats.MaxProfitPercent:F2}%)" +
                    $"\n   Макс. убыток: {positionStats.MaxLossCurrency:F2} ({positionStats.MaxLossPercent:F2}%)" +
                    $"\n   Уровень безубытка: {positionStats.BreakEvenPrice:F4}" +
                    $"\n   Мин. цена прибыли: {positionStats.MinProfitPrice:F4}";
            }

            SendNewLogMessage(logMessage, LogMessageType.Trade);
        }

        private void LogPositionOpened(Position position, PositionType type)
        {
            string positionType = type == PositionType.Bot ? "БОТ" : "РУЧНАЯ";
            
            SendNewLogMessage(
                $"🎯 {positionType} ПОЗИЦИЯ ОТКРЫТА #{GetPositionId(position)}\n" +
                $"Инструмент: {position.SecurityName}\n" +
                $"Направление: {position.Direction}\n" +
                $"Цена входа: {position.EntryPrice:F4}\n" +
                $"Объем: {Math.Abs(position.OpenVolume):F2}\n" +
                $"Время открытия: {position.TimeOpen}",
                LogMessageType.System);
        }

        private void LogPositionClosed(Position position)
        {
            TimeSpan lifeTime = DateTime.Now - position.TimeOpen;
            
            SendNewLogMessage(
                $"🏁 ПОЗИЦИЯ ЗАКРЫТА #{GetPositionId(position)}\n" +
                $"Инструмент: {position.SecurityName}\n" +
                $"Направление: {position.Direction}\n" +
                $"Цена входа: {position.EntryPrice:F4}\n" +
                $"Цена выхода: {position.ClosePrice:F4}\n" +
                $"Время жизни: {lifeTime:hh\\:mm\\:ss}",
                LogMessageType.System);
        }

        private void LogDetailedPositionStatistics()
        {
            if (_positionManager != null)
            {
                _positionManager.LogDetailedStatistics();
            }
        }
        #endregion

        #region ПУБЛИЧНЫЕ МЕТОДЫ
        public void ScanDataFiles()
        {
            try
            {
                if (_instrumentManager != null)
                {
                    bool changes = _instrumentManager.ScanDataFiles();
                    if (changes)
                    {
                        SendNewLogMessage("✅ Сканирование данных завершено. Обнаружены изменения.", 
                                        LogMessageType.System);
                    }
                    else
                    {
                        SendNewLogMessage("✅ Сканирование данных завершено. Изменений не обнаружено.", 
                                        LogMessageType.System);
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка сканирования данных: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        public void SetGlobalLearning(bool enable)
        {
            try
            {
                if (EnableGlobalLearning != null)
                {
                    EnableGlobalLearning.ValueBool = enable;
                }
                
                if (_trailingComponent != null)
                {
                    _trailingComponent.SetGlobalLearning(enable);
                }
                
                SendNewLogMessage(
                    enable 
                        ? "✅ Глобальное обучение трейлинг-стопа включено" 
                        : "⚠️ Глобальное обучение трейлинг-стопа отключено",
                    LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка переключения обучения: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        public void StartManualOptimization()
        {
            try
            {
                SendNewLogMessage("🚀 === ЗАПУСК РУЧНОЙ AI ОПТИМИЗАЦИИ ===", LogMessageType.System);

                // Проверка настроек оптимизации
                SendNewLogMessage($"📊 Режим оптимизации: {AiOptimizationMode?.ValueString}", LogMessageType.System);
                SendNewLogMessage($"🔄 Непрерывная оптимизация: {ContinuousOptimization?.ValueBool}", LogMessageType.System);
                SendNewLogMessage($"📈 Автооптимизация PSO: {PsoAutoOptimize?.ValueBool}", LogMessageType.System);

                // Проверка данных
                if (_aiEngine != null)
                {
                    SendNewLogMessage($"📁 Статус данных: {_aiEngine.GetOptimizationStatus()}", LogMessageType.System);
                    SendNewLogMessage($"📊 Текущий символ: {_aiEngine.GetCurrentSymbol()}", LogMessageType.System);

                    var availableSymbols = _aiEngine.GetAvailableSymbols();
                    SendNewLogMessage($"📈 Доступные символы: {string.Join(", ", availableSymbols.Take(5))}...", LogMessageType.System);

                    _aiEngine.StartOptimization();
                    SendNewLogMessage("✅ Ручная оптимизация запущена успешно!", LogMessageType.System);
                }
                else
                {
                    SendNewLogMessage("❌ AI Engine не инициализирован", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка запуска ручной оптимизации: {ex.Message}",
                                LogMessageType.Error);
            }
        }

        public void ForceOptimizationStart()
        {
            try
            {
                SendNewLogMessage("🔧 === ПРИНУДИТЕЛЬНЫЙ ЗАПУСК ОПТИМИЗАЦИИ ===", LogMessageType.System);

                if (_aiEngine == null)
                {
                    SendNewLogMessage("❌ AI Engine не найден", LogMessageType.Error);
                    return;
                }

                // Принудительная настройка параметров
                if (AiOptimizationMode != null)
                {
                    AiOptimizationMode.ValueString = "Гибридная";
                    SendNewLogMessage("✅ Режим оптимизации установлен: Гибридная", LogMessageType.System);
                }

                if (ContinuousOptimization != null)
                {
                    ContinuousOptimization.ValueBool = true;
                    SendNewLogMessage("✅ Непрерывная оптимизация включена", LogMessageType.System);
                }

                // Проверка и загрузка данных
                if (!_aiEngine.HasHistoricalData())
                {
                    SendNewLogMessage("⚠️ Исторические данные не найдены, сканируем...", LogMessageType.System);
                    ScanDataFiles();
                }

                // Запуск оптимизации
                _aiEngine.StartOptimization();
                SendNewLogMessage("🚀 Принудительная оптимизация запущена!", LogMessageType.System);

            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка принудительной оптимизации: {ex.Message}", LogMessageType.Error);
            }
        }

        public void DebugOptimizationStatus()
        {
            try
            {
                SendNewLogMessage("🔍 === ДИАГНОСТИКА ОПТИМИЗАЦИИ ===", LogMessageType.System);

                // Проверка компонентов
                SendNewLogMessage($"🤖 AI Engine: {(_aiEngine != null ? "Инициализирован" : "НЕТ")}", LogMessageType.System);
                SendNewLogMessage($"📊 Hybrid Engine: {_aiEngine?.GetOptimizationStatus()}", LogMessageType.System);

                // Проверка настроек
                SendNewLogMessage($"🎯 AiOptimizationMode: {AiOptimizationMode?.ValueString}", LogMessageType.System);
                SendNewLogMessage($"🔄 ContinuousOptimization: {ContinuousOptimization?.ValueBool}", LogMessageType.System);
                SendNewLogMessage($"⚙️ PsoAutoOptimize: {PsoAutoOptimize?.ValueBool}", LogMessageType.System);

                // Проверка данных
                if (_instrumentManager != null)
                {
                    var metrics = _instrumentManager.GetDetailedDataMetrics();
                    SendNewLogMessage($"📁 Всего файлов: {metrics["TotalFiles"]}", LogMessageType.System);
                    SendNewLogMessage($"📊 Всего символов: {metrics["TotalSymbols"]}", LogMessageType.System);
                    SendNewLogMessage($"⭐ Среднее качество: {metrics["AverageQuality"]}", LogMessageType.System);
                }

                SendNewLogMessage("===================================", LogMessageType.System);

            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка диагностики: {ex.Message}", LogMessageType.Error);
            }
        }

        public void CaptureManualPosition(Position position)
        {
            try
            {
                _positionManager?.CaptureManualPosition(position);
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"❌ Ошибка подхвата ручной позиции: {ex.Message}", 
                                LogMessageType.Error);
            }
        }

        public override string GetNameStrategyType() => "PSOScreenerHybridPro";

        public override void ShowIndividualSettingsDialog()
        {
            base.ShowIndividualSettingsDialog();
        }

        public void CleanupResources()
        {
            _assembly?.Cleanup();
            _stateMachine?.TransitionTo(AdaptiveTradingStateMachine.TradingState.Paused);
            
            if (AutoExportResults != null && AutoExportResults.ValueBool)
            {
                SendNewLogMessage("💾 Автоэкспорт результатов выполнен", LogMessageType.System);
            }
            
            SendNewLogMessage("🧹 Ресурсы бота очищены", LogMessageType.System);
        }
        
        public void DisposeResources()
        {
            CleanupResources();
        }
        #endregion
    }
    #endregion
}