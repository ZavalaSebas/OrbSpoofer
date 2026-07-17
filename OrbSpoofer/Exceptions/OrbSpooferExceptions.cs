namespace OrbSpoofer.Exceptions;

public class OrbSpooferException : Exception
{
    public OrbSpooferException(string message) : base(message) { }
    public OrbSpooferException(string message, Exception inner) : base(message, inner) { }
}

public class NetworkError : OrbSpooferException
{
    public NetworkError(string message) : base(message) { }
    public NetworkError(string message, Exception inner) : base(message, inner) { }
}

public class DatabaseLoadError : OrbSpooferException
{
    public DatabaseLoadError(string message) : base(message) { }
}
