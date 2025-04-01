using libplctag;
using libplctag.DataTypes;

class TagAttributeStub(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol) : TagAttribute(tagInfo, address, plc_type, plc_protocol)
{
    public override void InitializeTag()
    {
        // Stub implementation
    }

    public override void ReadTag()
    {
        // Stub implementation
    }

    public override double GetDoubleTagValue(int offset = 0)
    {
        return 1.2; // Stub implementation
    }

    public override bool GetBoolTagValue(int offset = 0)
    {
        return false; // Stub implementation
    }

    public override int GetSintTagValue(int offset = 0)
    {
        return 1; // Stub implementation
    }

    public override int GetIntTagValue(int offset = 0)
    {
        return 2; // Stub implementation
    }

    public override int GetDintTagValue(int offset = 0)
    {
        return 3;
    }
    public override string GetStringTagValue(int offset = 0)
    {
        return "dummy";
    }
}