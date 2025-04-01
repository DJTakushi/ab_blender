interface ITagAttribute
{
    public void InitializeTag();
    public void ReadTag(); 
    public double GetDoubleTagValue(int offset = 0);
    public bool GetBoolTagValue(int offset = 0);
    public int GetSintTagValue(int offset = 0);
    public int GetIntTagValue(int offset = 0);
    public int GetDintTagValue(int offset = 0);
    public string GetStringTagValue(int offset = 0);
}