namespace SmartX.Domain.Sensors;

/// <summary>
/// Aggregatable smart-meter sample. Operator overloads let gateway code write
/// <c>Meter3 = Meter1 + Meter2</c> without manual wattage arithmetic.
/// </summary>
public readonly struct SmartMeterReading : IEquatable<SmartMeterReading>
{
    public string MeterId { get; }
    public int Watts { get; }
    public float PowerFactor { get; }

    public SmartMeterReading(string meterId, int watts, float powerFactor = 1f)
    {
        MeterId = meterId;
        Watts = watts;
        PowerFactor = powerFactor;
    }

    public static SmartMeterReading operator +(SmartMeterReading left, SmartMeterReading right)
    {
        var combinedWatts = left.Watts + right.Watts;
        var weightedPf = combinedWatts == 0
            ? 0f
            : (left.Watts * left.PowerFactor + right.Watts * right.PowerFactor) / combinedWatts;
        return new SmartMeterReading($"{left.MeterId}+{right.MeterId}", combinedWatts, weightedPf);
    }

    public static SmartMeterReading operator -(SmartMeterReading left, SmartMeterReading right)
        => new($"{left.MeterId}-{right.MeterId}", left.Watts - right.Watts, left.PowerFactor);

    public static bool operator >(SmartMeterReading left, SmartMeterReading right) => left.Watts > right.Watts;
    public static bool operator <(SmartMeterReading left, SmartMeterReading right) => left.Watts < right.Watts;
    public static bool operator >=(SmartMeterReading left, SmartMeterReading right) => left.Watts >= right.Watts;
    public static bool operator <=(SmartMeterReading left, SmartMeterReading right) => left.Watts <= right.Watts;
    public static bool operator ==(SmartMeterReading left, SmartMeterReading right) => left.Equals(right);
    public static bool operator !=(SmartMeterReading left, SmartMeterReading right) => !left.Equals(right);

    public bool Equals(SmartMeterReading other) => Watts == other.Watts && MeterId == other.MeterId;
    public override bool Equals(object? obj) => obj is SmartMeterReading other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(MeterId, Watts);
    public override string ToString() => $"{MeterId}:{Watts}W";
}

/// <summary>
/// Hydroponic moisture sample. Addition averages two probes in the same zone.
/// </summary>
public readonly struct MoistureReading : IEquatable<MoistureReading>
{
    public float Percent { get; }

    public MoistureReading(float percent) => Percent = percent;

    public static MoistureReading operator +(MoistureReading left, MoistureReading right)
        => new((left.Percent + right.Percent) / 2f);

    public static MoistureReading operator -(MoistureReading left, MoistureReading right)
        => new(left.Percent - right.Percent);

    public static bool operator >(MoistureReading left, MoistureReading right) => left.Percent > right.Percent;
    public static bool operator <(MoistureReading left, MoistureReading right) => left.Percent < right.Percent;
    public static bool operator ==(MoistureReading left, MoistureReading right) => left.Equals(right);
    public static bool operator !=(MoistureReading left, MoistureReading right) => !left.Equals(right);

    public bool Equals(MoistureReading other) => Math.Abs(Percent - other.Percent) < 0.001f;
    public override bool Equals(object? obj) => obj is MoistureReading other && Equals(other);
    public override int GetHashCode() => Percent.GetHashCode();
}

/// <summary>
/// Boolean actuator (valve / relay) with logical operators for mesh-wide AND/OR checks.
/// </summary>
public readonly struct ActuatorState : IEquatable<ActuatorState>
{
    public bool IsEnergised { get; }

    public ActuatorState(bool isEnergised) => IsEnergised = isEnergised;

    public static ActuatorState operator &(ActuatorState left, ActuatorState right)
        => new(left.IsEnergised && right.IsEnergised);

    public static ActuatorState operator |(ActuatorState left, ActuatorState right)
        => new(left.IsEnergised || right.IsEnergised);

    public static bool operator ==(ActuatorState left, ActuatorState right) => left.Equals(right);
    public static bool operator !=(ActuatorState left, ActuatorState right) => !left.Equals(right);
    public static bool operator true(ActuatorState state) => state.IsEnergised;
    public static bool operator false(ActuatorState state) => !state.IsEnergised;

    public bool Equals(ActuatorState other) => IsEnergised == other.IsEnergised;
    public override bool Equals(object? obj) => obj is ActuatorState other && Equals(other);
    public override int GetHashCode() => IsEnergised.GetHashCode();
}
