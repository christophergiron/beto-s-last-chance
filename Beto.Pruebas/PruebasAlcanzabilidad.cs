using Beto.Motor.Finales;
using Beto.Motor.Narrativa;
using Beto.Motor.Nucleo;
using Xunit;

public class PruebasAlcanzabilidad
{
    static readonly ResultadoAlcanzabilidad Resultado =
        Alcanzabilidad.Analizar(ReglasBeto.Construir(semilla: 1));

    [Theory]
    [InlineData(Fase.F1_AprobacionMinima)]
    [InlineData(Fase.F2_MaximoDesempeno)]
    [InlineData(Fase.F3_Reprobacion)]
    [InlineData(Fase.F4_EventoSalud)]
    [InlineData(Fase.F5_ExcesoOcio)]
    public void Cada_final_es_alcanzable(Fase final)
    {
        Assert.True(Resultado.SecuenciasMinimas.ContainsKey(final), $"{final} NO es alcanzable");
    }

    [Fact]
    public void El_juego_nunca_se_atasca()
    {
        Assert.Equal(0, Resultado.Callejones);
    }

    [Fact]
    public void Exceso_de_ocio_son_exactamente_5_salidas_seguidas()
    {
        var seq = Resultado.SecuenciasMinimas[Fase.F5_ExcesoOcio];
        Assert.Equal(5, seq.Count);
        Assert.All(seq, a => Assert.Equal(AccionJugador.SalirConAmigos, a));
    }
}