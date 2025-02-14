// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe
// Author           : Felipe Silveira (Transis Software)
// Created          : 08-04-2022
//
// Last Modified By : Felipe Silveira (Transis Software)
// Last Modified On : 08-04-2022
// ***********************************************************************
// <copyright file="ProviderSystemPro.cs" company="OpenAC .Net">
//		        		   The MIT License (MIT)
//	     		Copyright (c) 2014 - 2024 Projeto OpenAC .Net
//
//	 Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//	 The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//	 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary></summary>
// ***********************************************************************

using OpenAC.Net.Core.Extensions;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFSe.Configuracao;
using OpenAC.Net.NFSe.Nota;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.NFSe.Commom;
using OpenAC.Net.NFSe.Commom.Interface;
using OpenAC.Net.NFSe.Commom.Model;
using OpenAC.Net.NFSe.Commom.Types;

namespace OpenAC.Net.NFSe.Providers;

internal sealed class ProviderMitra : ProviderABRASF201
{
    #region Constructors

    public ProviderMitra(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
    {
        Name = "Mitra";
    }

    #endregion Constructors

    #region Methods

    #region Protected Methods

    protected override string GerarCabecalho()
    {
        var cabecalho = new StringBuilder();
        cabecalho.Append("<cabecalho versao=\"2.01\" xmlns=\"http://www.abrasf.org.br/nfse.xsd\">");
        cabecalho.Append("<versaoDados>2.01</versaoDados>");
        cabecalho.Append("</cabecalho>");
        return cabecalho.ToString();
    }

    protected override void AssinarEnviar(RetornoEnviar retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXml(retornoWebservice.XmlEnvio, "EnviarLoteRpsEnvio", "LoteRps", Certificado);
    }

    protected override void AssinarEnviarSincrono(RetornoEnviar retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXml(retornoWebservice.XmlEnvio, "EnviarLoteRpsSincronoEnvio", "LoteRps", Certificado);
    }

    protected override IServiceClient GetClient(TipoUrl tipo) => new MitraServiceClient(this, tipo, Certificado);

    #endregion Protected Methods

    #region RPS

    protected override XElement WriteServicosRps(NotaServico nota)
    {
        var servico = new XElement("Servico");

        servico.Add(WriteValoresRps(nota));

        servico.AddChild(AddTag(TipoCampo.Int, "", "IssRetido", 1, 1, Ocorrencia.Obrigatoria, nota.Servico.Valores.IssRetido == SituacaoTributaria.Retencao ? 1 : 2));

        if (nota.Servico.ResponsavelRetencao.HasValue)
            servico.AddChild(AddTag(TipoCampo.Int, "", "ResponsavelRetencao", 1, 1, Ocorrencia.NaoObrigatoria, (int)nota.Servico.ResponsavelRetencao + 1));

        servico.AddChild(AddTag(TipoCampo.Str, "", "ItemListaServico", 1, 5, Ocorrencia.Obrigatoria, nota.Servico.ItemListaServico));
        servico.AddChild(AddTag(TipoCampo.Str, "", "Discriminacao", 1, 2000, Ocorrencia.Obrigatoria, nota.Servico.Discriminacao));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoMunicipio", 1, 20, Ocorrencia.Obrigatoria, nota.Servico.CodigoMunicipio));
        servico.AddChild(AddTag(TipoCampo.Int, "", "ExigibilidadeISS", 1, 1, Ocorrencia.Obrigatoria, (int)nota.Servico.ExigibilidadeIss + 1));
        servico.AddChild(AddTag(TipoCampo.Int, "", "MunicipioIncidencia", 7, 7, Ocorrencia.MaiorQueZero, nota.Servico.MunicipioIncidencia));

        return servico;
    }

    #endregion RPS

    #region Services

    protected override void PrepararConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
    {
        var loteBuilder = new StringBuilder();
        loteBuilder.Append($"<ConsultarNfseFaixaEnvio {GetNamespace()}>");
        loteBuilder.Append("<Prestador>");
        loteBuilder.Append("<CpfCnpj>");
        loteBuilder.Append(Configuracoes.PrestadorPadrao.CpfCnpj.IsCNPJ()
            ? $"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(14)}</Cnpj>"
            : $"<Cpf>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(11)}</Cpf>");
        loteBuilder.Append("</CpfCnpj>");
        if (!Configuracoes.PrestadorPadrao.InscricaoMunicipal.IsEmpty()) loteBuilder.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
        loteBuilder.Append("</Prestador>");

        loteBuilder.Append("<Faixa>");
        if (retornoWebservice.NumeroNFse > 0)
        {
            loteBuilder.Append($"<NumeroNfseInicial>{retornoWebservice.NumeroNFse}</NumeroNfseInicial>");
            loteBuilder.Append($"<NumeroNfseFinal>{retornoWebservice.NumeroNFse}</NumeroNfseFinal>");
        }
        loteBuilder.Append("</Faixa>");
        loteBuilder.Append($"<Pagina>{System.Math.Max(retornoWebservice.Pagina, 1)}</Pagina>");
        loteBuilder.Append("</ConsultarNfseFaixaEnvio>");

        retornoWebservice.XmlEnvio = loteBuilder.ToString();
    }

    #endregion Services

    #endregion Methods
}