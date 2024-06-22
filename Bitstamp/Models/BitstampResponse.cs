namespace MilkerTools.Models;

public class BitstampResponse<T> : BitstampResponse
{
    public new T? Content { get; set; }

    public BitstampResponse(BitstampError error) : base(error)
    {
        Error = error;
    }

    public BitstampResponse(T content) : base(content)
    {
        Content = content;
    }
}

public class BitstampResponse
{
    public BitstampError? Error { get; set; }
    public object? Content { get; set; }
    public bool Success => Error == null;

    public BitstampResponse(object? content)
    {
        if (content is BitstampError error)
        {
            Error = error;
        }
        else
        {
            Content = content;
        }
    }
}

public class BitstampErrorResponse<T>(BitstampError error) : BitstampResponse<T>(error)
{
}

public class BitstampErrorResponse(BitstampError error) : BitstampResponse(error)
{
}