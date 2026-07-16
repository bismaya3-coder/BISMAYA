# BISMAYA Trading Bots

This repository contains a cTrader Automate (cAlgo) robot for automatic XAUUSD trading.

## XAUUSD Multi-Timeframe TRIX Bot

The bot file is located at:

```text
cTrader/Robots/XauUsdMultiTimeframeTrixBot.cs
```

### Strategy rules

The default configuration follows these bullish XAUUSD entry rules:

- The 15-minute TRIX is rising, confirming bullish trend direction.
- The 5-minute TRIX is rising, confirming entry timing aligns with the trend.
- The 5-minute TRIX is above its signal line, or has just crossed above it.
- Price is above a configurable moving average and/or key support zone.
- Tick volume is above its configurable average by a configurable multiplier.
- Position size starts at `0.01` lots and is fully configurable.

Optional sell logic is also included and can be enabled with the `Trade Direction` parameter.

### Important configurable parameters

Every major input is exposed as a cTrader parameter, including:

- Symbol name, trade direction, lots, label, and maximum positions.
- Entry and trend timeframes.
- TRIX period, signal period, slope lookback, and fresh-cross behavior.
- Moving-average period, support/resistance lookback, and support buffer.
- Volume average period and volume multiplier.
- Stop-loss, take-profit, trailing stop, max spread, and bar-close execution mode.

### How to use in cTrader

1. Open cTrader Automate.
2. Create a new cBot.
3. Replace the generated code with `cTrader/Robots/XauUsdMultiTimeframeTrixBot.cs`.
4. Build the cBot.
5. Attach it to an XAUUSD chart.
6. Review all parameters before enabling live trading.

> Trading leveraged gold products is risky. Backtest and forward-test on a demo account before using real funds.
