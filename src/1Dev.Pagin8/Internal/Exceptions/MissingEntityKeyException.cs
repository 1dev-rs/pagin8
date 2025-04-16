using System;

namespace Tar.Rest.LibShared.Internal.Exceptions;
public class MissingEntityKeyException(string message) : Exception(message);

