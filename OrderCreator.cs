using TradingPlatform.BusinessLayer;
using IOrder = InterestZones.Shared.IOrder;

namespace InterestZones;

public interface OrderCreator
{
    IOrder CreateOrder(Fractal fractal, Symbol symbol, double rectangleHigh);
}