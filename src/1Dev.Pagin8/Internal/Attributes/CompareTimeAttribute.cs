namespace _1Dev.Pagin8.Internal.Attributes;

/// <summary>
/// A <see cref="DateTime"/> property whose filters keep the time of the value. Without it a
/// comparison takes the date part of both sides (<c>DATE(col) op DATE(value)</c>). With it the value
/// is the interval of its own precision: a day for <c>yyyy-MM-dd</c>, a minute for
/// <c>yyyy-MM-ddTHH:mm</c>, a second for <c>yyyy-MM-ddTHH:mm:ss</c>, a millisecond for
/// <c>yyyy-MM-ddTHH:mm:ss.fff</c>; <c>eq</c> matches inside it, <c>gte</c> / <c>lt</c> compare with
/// its start and <c>gt</c> / <c>lte</c> with its end. A value without a time therefore matches the
/// same rows as without the attribute. Column metadata carries the flag <c>compare-time</c>, so a
/// client can offer a time picker for this property only. Ignored on a <see cref="DateOnly"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CompareTimeAttribute : Attribute;
