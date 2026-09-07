namespace GestorONG.Contracts.V1;

public sealed record DoacaoRecebidaEvent
{
    public required Guid IdDoacao { get; init; }

    public required Guid IdCampanha { get; init; }

    public required Guid IdDoador { get; init; }

    public required decimal Valor { get; init; }

    public required DateTimeOffset OcorridoEm { get; init; }
}
