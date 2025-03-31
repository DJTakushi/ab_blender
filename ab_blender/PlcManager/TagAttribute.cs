
using libplctag;
using libplctag.DataTypes;
public enum TagType // TODO : confirm these values
{
    BOOL,
    SINT = 193, //  193, addr, 3, 1 
    INT = 195, // 195, adr, 3,2
    DINT = 196, // expressed by vars with "Dint" in their name
    REAL = 202, // confirmed in the field
    STRING = 33633
}

// TODO : make an interface and factory to replace stubbing
class TagAttribute(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol)
{
    public TagInfo TagInfo = tagInfo;
    private readonly Tag Tag = new Tag
    {
        Name = $"{tagInfo.Name}",
        Path = "1,0", // assuming default,
        Gateway = address,
        PlcType = plc_type,
        Protocol = plc_protocol
    };
    public void InitializeTag()
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            Tag.Initialize();
        }
    }
    public void ReadTag()
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            Tag.Read();
        }
    }
    public double GetDoubleTagValue(int offset = 0)
    {
        return Tag.GetFloat32(offset);
    }
    public bool GetBoolTagValue(int offset = 0)
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            return Tag.GetBit(0);
        }
        else
        {
            return false;
        }
    }
    public int GetSintTagValue(int offset = 0)
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            return Tag.GetInt8(offset);
        }
        else
        {
            return 0;
        }
    }
    public int GetIntTagValue(int offset = 0)
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            return Tag.GetInt16(offset);
        }
        else
        {
            return 0;
        }
    }
    public int GetDintTagValue(int offset = 0)
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            return Tag.GetInt32(offset);
        }
        else
        {
            return 0;
        }
    }
    public string GetStringTagValue(int offset = 0)
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            return Tag.GetString(offset);
        }
        else
        {
            return string.Empty;
        }
    }
}
