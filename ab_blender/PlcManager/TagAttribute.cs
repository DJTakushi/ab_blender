
using libplctag;
using libplctag.DataTypes;

// TODO : make an interface and factory to replace stubbing
class TagAttribute(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol) : ITagAttribute
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
    public virtual void InitializeTag()
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            Tag.Initialize();
        }
    }
    public virtual void ReadTag()
    {
        if (!EnvVarHelper.GetPlcStub())
        {
            Tag.Read();
        }
    }
    public virtual double GetDoubleTagValue(int offset = 0)
    {
        return Tag.GetFloat32(offset);
    }
    public virtual bool GetBoolTagValue(int offset = 0)
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
    public virtual int GetSintTagValue(int offset = 0)
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
    public virtual int GetIntTagValue(int offset = 0)
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
    public virtual int GetDintTagValue(int offset = 0)
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
    public virtual string GetStringTagValue(int offset = 0)
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
