using InterestZones;
using TradingPlatform.BusinessLayer;

namespace InterestZones;

public interface Bot
{
    Bar GetBar(int index);
}