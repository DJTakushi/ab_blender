using libplctag;
using libplctag.DataTypes;

class TagAttributeStub : TagAttribute
{
    public TagAttributeStub(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol) : base(tagInfo, address, plc_type, plc_protocol)
    {
        // Constructor logic for the stub
    }

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
        return 0.0; // Stub implementation
    }

    public override bool GetBoolTagValue(int offset = 0)
    {
        return false; // Stub implementation
    }

    public override int GetSintTagValue(int offset = 0)
    {
        return 0; // Stub implementation
    }

    public override int GetIntTagValue(int offset = 0)
    {
        return 0; // Stub implementation
    }

    public override int GetDintTagValue(int offset = 0)
    {
        return 0;
    }
    public override string GetStringTagValue(int offset = 0)
    {
        return string.Empty;
    }
}