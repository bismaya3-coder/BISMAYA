// XAUUSD Multi-Timeframe TRIX Bot for cTrader Automate (cAlgo)
// Attach to an XAUUSD chart. Defaults are aligned with the screenshot: 0.01 lots,
// 20 pip minimum SL/TP distances, and 5-minute entries confirmed by 15-minute trend.

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdMultiTimeframeTrixBot : Robot
    {
        [Parameter("Symbol Name", DefaultValue = "XAUUSD", Group = "Trading")]
        public string TradingSymbolName { get; set; }

        [Parameter("Trade Direction", DefaultValue = TradeDirectionMode.BuyOnly, Group = "Trading")]
        public TradeDirectionMode TradeDirection { get; set; }

        [Parameter("Lots", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group = "Trading")]
        public double Lots { get; set; }

        [Parameter("Max Positions", DefaultValue = 1, MinValue = 1, Group = "Trading")]
        public int MaxPositions { get; set; }

        [Parameter("Label", DefaultValue = "XAUUSD_TRIX_MTF", Group = "Trading")]
        public string Label { get; set; }

        [Parameter("Entry Timeframe", DefaultValue = "Minute5", Group = "Timeframes")]
        public TimeFrame EntryTimeFrame { get; set; }

        [Parameter("Trend Timeframe", DefaultValue = "Minute15", Group = "Timeframes")]
        public TimeFrame TrendTimeFrame { get; set; }

        [Parameter("TRIX Period", DefaultValue = 14, MinValue = 2, Group = "TRIX")]
        public int TrixPeriod { get; set; }

        [Parameter("TRIX Signal Period", DefaultValue = 9, MinValue = 1, Group = "TRIX")]
        public int TrixSignalPeriod { get; set; }

        [Parameter("Slope Lookback Bars", DefaultValue = 1, MinValue = 1, Group = "TRIX")]
        public int SlopeLookbackBars { get; set; }

        [Parameter("Require Fresh Cross", DefaultValue = false, Group = "TRIX")]
        public bool RequireFreshCross { get; set; }

        [Parameter("Fresh Cross Max Bars", DefaultValue = 2, MinValue = 1, Group = "TRIX")]
        public int FreshCrossMaxBars { get; set; }

        [Parameter("Use Moving Average Filter", DefaultValue = true, Group = "Price Filter")]
        public bool UseMovingAverageFilter { get; set; }

        [Parameter("MA Period", DefaultValue = 50, MinValue = 2, Group = "Price Filter")]
        public int MovingAveragePeriod { get; set; }

        [Parameter("Use Support Filter", DefaultValue = true, Group = "Price Filter")]
        public bool UseSupportFilter { get; set; }

        [Parameter("Support Lookback Bars", DefaultValue = 20, MinValue = 2, Group = "Price Filter")]
        public int SupportLookbackBars { get; set; }

        [Parameter("Support Buffer Pips", DefaultValue = 5, MinValue = 0, Group = "Price Filter")]
        public double SupportBufferPips { get; set; }

        [Parameter("Use Volume Filter", DefaultValue = true, Group = "Volume")]
        public bool UseVolumeFilter { get; set; }

        [Parameter("Volume Average Period", DefaultValue = 20, MinValue = 2, Group = "Volume")]
        public int VolumeAveragePeriod { get; set; }

        [Parameter("Volume Multiplier", DefaultValue = 1.10, MinValue = 0.1, Step = 0.05, Group = "Volume")]
        public double VolumeMultiplier { get; set; }

        [Parameter("Stop Loss Pips", DefaultValue = 20, MinValue = 0, Group = "Risk")]
        public double StopLossPips { get; set; }

        [Parameter("Take Profit Pips", DefaultValue = 40, MinValue = 0, Group = "Risk")]
        public double TakeProfitPips { get; set; }

        [Parameter("Use Trailing Stop", DefaultValue = false, Group = "Risk")]
        public bool UseTrailingStop { get; set; }

        [Parameter("Trailing Stop Pips", DefaultValue = 20, MinValue = 1, Group = "Risk")]
        public double TrailingStopPips { get; set; }

        [Parameter("Close On Opposite Signal", DefaultValue = true, Group = "Exits")]
        public bool CloseOnOppositeSignal { get; set; }

        [Parameter("Max Spread Pips", DefaultValue = 5, MinValue = 0, Group = "Safety")]
        public double MaxSpreadPips { get; set; }

        [Parameter("Trade On Bar Close Only", DefaultValue = true, Group = "Safety")]
        public bool TradeOnBarCloseOnly { get; set; }

        private Symbol _symbol;
        private Bars _entryBars;
        private Bars _trendBars;

        protected override void OnStart()
        {
            _symbol = Symbols.GetSymbol(TradingSymbolName);
            _entryBars = MarketData.GetBars(EntryTimeFrame, TradingSymbolName);
            _trendBars = MarketData.GetBars(TrendTimeFrame, TradingSymbolName);

            Print("{0} started for {1}. Lots={2}, entry TF={3}, trend TF={4}.", Label, TradingSymbolName, Lots, EntryTimeFrame, TrendTimeFrame);
        }

        protected override void OnBar()
        {
            if (TradeOnBarCloseOnly)
                Evaluate();
        }

        protected override void OnTick()
        {
            if (UseTrailingStop)
                ManageTrailingStops();

            if (!TradeOnBarCloseOnly)
                Evaluate();
        }

        private void Evaluate()
        {
            if (_symbol == null || SymbolName != TradingSymbolName)
                return;

            if (_symbol.Spread / _symbol.PipSize > MaxSpreadPips)
                return;

            var buySignal = IsBullishSetup();
            var sellSignal = AllowsSell() && IsBearishSetup();

            if (CloseOnOppositeSignal)
                CloseOppositePositions(buySignal, sellSignal);

            if (buySignal && AllowsBuy() && CountPositions(TradeType.Buy) < MaxPositions)
                OpenPosition(TradeType.Buy);

            if (sellSignal && AllowsSell() && CountPositions(TradeType.Sell) < MaxPositions)
                OpenPosition(TradeType.Sell);
        }

        private bool IsBullishSetup()
        {
            return IsTrixRising(_trendBars) && IsTrixRising(_entryBars) && IsTrixAboveOrCrossedAbove(_entryBars) && IsPriceAboveFilter(_entryBars) && IsVolumeIncreasing(_entryBars);
        }

        private bool IsBearishSetup()
        {
            return IsTrixFalling(_trendBars) && IsTrixFalling(_entryBars) && IsTrixBelowOrCrossedBelow(_entryBars) && IsPriceBelowFilter(_entryBars) && IsVolumeIncreasing(_entryBars);
        }

        private bool IsTrixRising(Bars bars)
        {
            var trix = CalculateTrix(bars.ClosePrices);
            int i = trix.Length - 2;
            return i - SlopeLookbackBars >= 0 && trix[i] > trix[i - SlopeLookbackBars];
        }

        private bool IsTrixFalling(Bars bars)
        {
            var trix = CalculateTrix(bars.ClosePrices);
            int i = trix.Length - 2;
            return i - SlopeLookbackBars >= 0 && trix[i] < trix[i - SlopeLookbackBars];
        }

        private bool IsTrixAboveOrCrossedAbove(Bars bars)
        {
            var trix = CalculateTrix(bars.ClosePrices);
            var signal = CalculateEma(trix, TrixSignalPeriod);
            int i = trix.Length - 2;
            if (i < 1)
                return false;

            bool above = trix[i] > signal[i];
            bool crossed = CrossedAbove(trix, signal, i, FreshCrossMaxBars);
            return RequireFreshCross ? crossed : above || crossed;
        }

        private bool IsTrixBelowOrCrossedBelow(Bars bars)
        {
            var trix = CalculateTrix(bars.ClosePrices);
            var signal = CalculateEma(trix, TrixSignalPeriod);
            int i = trix.Length - 2;
            if (i < 1)
                return false;

            bool below = trix[i] < signal[i];
            bool crossed = CrossedBelow(trix, signal, i, FreshCrossMaxBars);
            return RequireFreshCross ? crossed : below || crossed;
        }

        private bool IsPriceAboveFilter(Bars bars)
        {
            int i = bars.Count - 2;
            if (i < Math.Max(MovingAveragePeriod, SupportLookbackBars))
                return false;

            double close = bars.ClosePrices[i];
            bool aboveMa = !UseMovingAverageFilter || close > SimpleAverage(bars.ClosePrices, i, MovingAveragePeriod);
            bool aboveSupport = !UseSupportFilter || close > LowestLow(bars, i, SupportLookbackBars) + SupportBufferPips * _symbol.PipSize;
            return aboveMa && aboveSupport;
        }

        private bool IsPriceBelowFilter(Bars bars)
        {
            int i = bars.Count - 2;
            if (i < Math.Max(MovingAveragePeriod, SupportLookbackBars))
                return false;

            double close = bars.ClosePrices[i];
            bool belowMa = !UseMovingAverageFilter || close < SimpleAverage(bars.ClosePrices, i, MovingAveragePeriod);
            bool belowResistance = !UseSupportFilter || close < HighestHigh(bars, i, SupportLookbackBars) - SupportBufferPips * _symbol.PipSize;
            return belowMa && belowResistance;
        }

        private bool IsVolumeIncreasing(Bars bars)
        {
            if (!UseVolumeFilter)
                return true;

            int i = bars.Count - 2;
            if (i < VolumeAveragePeriod)
                return false;

            double averageVolume = Enumerable.Range(i - VolumeAveragePeriod, VolumeAveragePeriod).Average(x => bars.TickVolumes[x]);
            return bars.TickVolumes[i] > averageVolume * VolumeMultiplier;
        }

        private void OpenPosition(TradeType tradeType)
        {
            double volume = _symbol.NormalizeVolumeInUnits(_symbol.QuantityToVolumeInUnits(Lots), RoundingMode.Down);
            double? stopLoss = StopLossPips > 0 ? StopLossPips : null;
            double? takeProfit = TakeProfitPips > 0 ? TakeProfitPips : null;

            var result = ExecuteMarketOrder(tradeType, TradingSymbolName, volume, Label, stopLoss, takeProfit);
            if (!result.IsSuccessful)
                Print("Order failed: {0}", result.Error);
        }

        private void CloseOppositePositions(bool buySignal, bool sellSignal)
        {
            foreach (var position in Positions.FindAll(Label, TradingSymbolName))
            {
                if ((position.TradeType == TradeType.Buy && sellSignal) || (position.TradeType == TradeType.Sell && buySignal))
                    ClosePosition(position);
            }
        }

        private void ManageTrailingStops()
        {
            foreach (var position in Positions.FindAll(Label, TradingSymbolName))
            {
                double distance = TrailingStopPips * _symbol.PipSize;
                if (position.TradeType == TradeType.Buy)
                {
                    double newStop = _symbol.Bid - distance;
                    if (!position.StopLoss.HasValue || newStop > position.StopLoss.Value)
                        ModifyPosition(position, newStop, position.TakeProfit, ProtectionType.Absolute);
                }
                else
                {
                    double newStop = _symbol.Ask + distance;
                    if (!position.StopLoss.HasValue || newStop < position.StopLoss.Value)
                        ModifyPosition(position, newStop, position.TakeProfit, ProtectionType.Absolute);
                }
            }
        }

        private int CountPositions(TradeType tradeType)
        {
            return Positions.FindAll(Label, TradingSymbolName).Count(p => p.TradeType == tradeType);
        }

        private bool AllowsBuy()
        {
            return TradeDirection == TradeDirectionMode.BuyOnly || TradeDirection == TradeDirectionMode.BuyAndSell;
        }

        private bool AllowsSell()
        {
            return TradeDirection == TradeDirectionMode.SellOnly || TradeDirection == TradeDirectionMode.BuyAndSell;
        }

        private double[] CalculateTrix(DataSeries prices)
        {
            var ema1 = CalculateEma(prices, TrixPeriod);
            var ema2 = CalculateEma(ema1, TrixPeriod);
            var ema3 = CalculateEma(ema2, TrixPeriod);
            var trix = new double[prices.Count];

            for (int i = 1; i < prices.Count; i++)
                trix[i] = Math.Abs(ema3[i - 1]) < double.Epsilon ? 0 : ((ema3[i] - ema3[i - 1]) / ema3[i - 1]) * 100.0;

            return trix;
        }

        private double[] CalculateEma(DataSeries data, int period)
        {
            var values = new double[data.Count];
            for (int i = 0; i < data.Count; i++)
                values[i] = data[i];
            return CalculateEma(values, period);
        }

        private double[] CalculateEma(double[] data, int period)
        {
            var ema = new double[data.Length];
            if (data.Length == 0)
                return ema;

            double multiplier = 2.0 / (period + 1);
            ema[0] = data[0];
            for (int i = 1; i < data.Length; i++)
                ema[i] = data[i] * multiplier + ema[i - 1] * (1 - multiplier);

            return ema;
        }

        private bool CrossedAbove(double[] first, double[] second, int index, int lookback)
        {
            for (int i = index; i > Math.Max(0, index - lookback); i--)
                if (first[i] > second[i] && first[i - 1] <= second[i - 1])
                    return true;
            return false;
        }

        private bool CrossedBelow(double[] first, double[] second, int index, int lookback)
        {
            for (int i = index; i > Math.Max(0, index - lookback); i--)
                if (first[i] < second[i] && first[i - 1] >= second[i - 1])
                    return true;
            return false;
        }

        private double SimpleAverage(DataSeries data, int index, int period)
        {
            return Enumerable.Range(index - period + 1, period).Average(i => data[i]);
        }

        private double LowestLow(Bars bars, int index, int period)
        {
            return Enumerable.Range(index - period + 1, period).Min(i => bars.LowPrices[i]);
        }

        private double HighestHigh(Bars bars, int index, int period)
        {
            return Enumerable.Range(index - period + 1, period).Max(i => bars.HighPrices[i]);
        }
    }

    public enum TradeDirectionMode
    {
        BuyOnly,
        SellOnly,
        BuyAndSell
    }
}
