namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public abstract class BooleanToken(bool value) : FilterToken
{
    public bool Value { get; set; } = value;
}