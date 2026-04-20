namespace _1Dev.Pagin8.Internal.Exceptions.Base;

public class Pagin8Exception : Exception
{
    public string Code { get; }

    public Pagin8Exception(string code) : base(code)
    {
        Code = code;
    }

    public Pagin8Exception(string code, string detail) : base($"{code}: {detail}")
    {
        Code = code;
    }
}
