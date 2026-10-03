using OrcPro.Domain.Common.Formatters;
using Xunit;

namespace OrcPro.Tests
{
    public class CpfCnpjTests
    {
        private const string CnpjNumericoValido = "11222333000181";
        private const string CpfValido = "52998224725";

        [Fact]
        public void CpfCnpjValidator_Normalizar_DeveRemoverMascara()
        {
            Assert.Equal(CnpjNumericoValido, CpfCnpjValidator.Normalizar("11.222.333/0001-81"));
            Assert.Equal(CpfValido, CpfCnpjValidator.Normalizar("529.982.247-25"));
        }

        [Fact]
        public void CpfCnpjValidator_Normalizar_DeveConverterLetrasParaMaiusculo()
        {
            var normalized = CpfCnpjValidator.Normalizar("fzl0jib7258a66");
            Assert.Equal(CnpjAlfaValido, normalized);
        }

        [Fact]
        public void CpfCnpjValidator_ValidarCpf_DeveSerValido()
        {
            var result = CpfCnpjValidator.Validar(CpfValido);
            Assert.True(result.IsValid);
            Assert.Equal(DocumentoTipo.Cpf, result.Tipo);
            Assert.Equal("529.982.247-25", result.FormattedValue);
        }

        [Fact]
        public void CpfCnpjValidator_ValidarCnpjNumerico_DeveSerValido()
        {
            var result = CpfCnpjValidator.Validar(CnpjNumericoValido);
            Assert.True(result.IsValid);
            Assert.Equal(DocumentoTipo.CnpjNumerico, result.Tipo);
            Assert.Equal("11.222.333/0001-81", result.FormattedValue);
        }

        [Theory]
        [InlineData("11.222.333/0001-81")]
        [InlineData("11222333000181")]
        public void CpfCnpjValidator_EhValido_ComMascaraOuSem_DeveRetornarTrue(string input)
        {
            Assert.True(CpfCnpjValidator.EhValido(input));
        }

        [Theory]
        [InlineData("52998224726")]
        [InlineData("11111111111")]
        [InlineData("123")]
        [InlineData("")]
        public void CpfCnpjValidator_EhValido_CpfInvalido_DeveRetornarFalse(string input)
        {
            Assert.False(CpfCnpjValidator.EhValido(input));
        }

        [Theory]
        [InlineData("11222333000199")]
        [InlineData("11111111111111")]
        [InlineData("123")]
        public void CpfCnpjValidator_EhValido_CnpjInvalido_DeveRetornarFalse(string input)
        {
            Assert.False(CpfCnpjValidator.EhValido(input));
        }

        [Fact]
        public void CpfCnpjValidator_EhValido_Null_DeveRetornarFalse()
        {
            Assert.False(CpfCnpjValidator.EhValido(null));
        }

        [Fact]
        public void CpfCnpjValidator_Compatibilidade_ComCpfsExistentes_DeveContinuarValidando()
        {
            Assert.True(CpfCnpjValidator.EhValido("11222333000181"));
            Assert.True(CpfCnpjValidator.EhValido("52998224725"));
        }

        // ===== CNPJ Alfanumérico Tests =====

        private const string CnpjAlfaValido = "FZL0JIB7258A66";
        private const string CnpjAlfaMascara = "FZ.L0J.IB7/258A-66";

        [Fact]
        public void CnpjAlfanumerico_Validar_DeveIdentificarComoCnpjAlfanumerico()
        {
            var result = CpfCnpjValidator.Validar(CnpjAlfaValido);
            Assert.Equal(DocumentoTipo.CnpjAlfanumerico, result.Tipo);
        }

        [Theory]
        [InlineData("FZ.L0J.IB7/258A-66")]
        [InlineData("FZL0JIB7258A66")]
        public void CnpjAlfanumerico_Formatar_DeveGerarMascaraCorreta(string input)
        {
            var formatted = CpfCnpjValidator.Formatar(input);
            Assert.Equal(CnpjAlfaMascara, formatted);
        }

        [Theory]
        [InlineData("FZ.L0J.IB7/258A-66")]
        [InlineData("FZL0JIB7258A66")]
        [InlineData("fzl0jib7258a66")]
        public void CnpjAlfanumerico_EntradaComESemMascara_DeveAceitar(string input)
        {
            Assert.True(CpfCnpjValidator.EhValido(input));
        }

        [Theory]
        [InlineData("fzl0jib7258a67")]  // dígito verificador errado
        public void CnpjAlfanumerico_DigitoVerificadorIncorreto_DeveRejeitar(string input)
        {
            Assert.False(CpfCnpjValidator.EhValido(input));
        }

        [Theory]
        [InlineData("FZ@L0JIB7258A66")] // caractere inválido (@)
        [InlineData("FZ#L0JIB7258A66")] // caractere inválido (#)
        public void CnpjAlfanumerico_CaracteresInvalidos_DeveRejeitar(string input)
        {
            Assert.False(CpfCnpjValidator.EhValido(input));
        }

        [Fact]
        public void CnpjAlfanumerico_UltimosDoisCaracteresDevemSerNumericos()
        {
            var withLettersInDv = "FZL0JIB7258A6A"; // A não é dígito
            Assert.False(CpfCnpjValidator.EhValido(withLettersInDv));
        }

        [Fact]
        public void CnpjAlfanumerico_DuplicidadeAposNormalizacao_DeveSerDetectada()
        {
            var cnpj1 = CnpjAlfaMascara;
            var cnpj2 = CnpjAlfaValido;

            var normalizado1 = CpfCnpjValidator.Normalizar(cnpj1);
            var normalizado2 = CpfCnpjValidator.Normalizar(cnpj2);

            Assert.Equal(normalizado1, normalizado2);
        }

        [Fact]
        public void CnpjAlfanumerico_Compatibilidade_ComCnjpjsNumericosExistentes()
        {
            Assert.True(CpfCnpjValidator.EhValido(CnpjNumericoValido));
            var result = CpfCnpjValidator.Validar(CnpjNumericoValido);
            Assert.Equal(DocumentoTipo.CnpjNumerico, result.Tipo);
        }

        [Fact]
        public void CnpjAlfanumerico_Formatar_DeveAplicarMascaraAA_AAA_AAA_AAAA_00()
        {
            var formatted = CpfCnpjValidator.Formatar(CnpjAlfaValido);
            Assert.Equal(CnpjAlfaMascara, formatted);
        }

        [Fact]
        public void CnpjAlfanumerico_GerarCnpjAlfanumericoValido_DeveSerValido()
        {
            var cnpjGerado = CpfCnpjValidator.GerarCnpjAlfanumericoValido();
            Assert.True(CpfCnpjValidator.EhValido(cnpjGerado));
        }
    }
}
