using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Collections.Generic;

public class Cielo
{
    public static Transacao IniciarPedido(string bandeira, string idioma, int moeda, int valor_pedido, string numero_pedido, string descricao_pedido, string url_retorno, bool capturar)
    {
        var xml_pedido = new XDocument(
            new XDeclaration("1.0", "ISO-8859-1", "yes"),
            new XElement("requisicao-transacao",
                new XAttribute("id", 1),
                new XAttribute("versao", "1.1.1"),
                new XElement("dados-ec",
                    new XElement("numero", ConfigurationManager.AppSettings["NUMERO_LOJA"]),
                    new XElement("chave", ConfigurationManager.AppSettings["CHAVE_LOJA"])),
                new XElement("dados-pedido",
                    new XElement("numero", numero_pedido),
                    new XElement("valor", valor_pedido),
                    new XElement("moeda", moeda),
                    new XElement("data-hora", DateTime.Now),
                    new XElement("descricao", descricao_pedido),
                    new XElement("idioma", idioma)),
                new XElement("forma-pagamento",
                    new XElement("bandeira", bandeira),
                    new XElement("produto", 1),
                    new XElement("parcelas", 1)),
                new XElement("url-retorno", url_retorno),
                new XElement("autorizar", 3),
                new XElement("capturar", capturar)));

        var xml = PostXML(ConfigurationManager.AppSettings["URL_CIELO"], xml_pedido);

        if (xml.Root.Name.LocalName == "erro")
            throw new Exception("Erro no Gateway Cielo: " + xml.Root.LocalElement("mensagem").Value);

        return new Transacao(xml);
    }

    public static Transacao Consulta(string tid)
    {
        var xml_autorizacao = new XDocument(
            new XDeclaration("1.0", "ISO-8859-1", "yes"),
            new XElement("requisicao-consulta",
                new XAttribute("id", 5),
                new XAttribute("versao", "1.1.1"),
                new XElement("tid", tid),
                new XElement("dados-ec",
                    new XElement("numero", ConfigurationManager.AppSettings["NUMERO_LOJA"]),
                    new XElement("chave", ConfigurationManager.AppSettings["CHAVE_LOJA"]))));

        var xml = PostXML(ConfigurationManager.AppSettings["URL_CIELO"], xml_autorizacao);

        if (xml.Root.Name.LocalName == "erro")
            throw new Exception("Erro no Gateway Cielo: " + xml.Root.LocalElement("mensagem").Value);

        return new Transacao(xml);
    }

    private static XDocument PostXML(string URL, XDocument Dados)
    {
        var encoding = new ASCIIEncoding();
        var data = encoding.GetBytes("mensagem=<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n" + Dados.ToString());

        var requisicao = WebRequest.Create(URL) as HttpWebRequest;
        requisicao.Method = "POST";
        requisicao.ContentType = "application/x-www-form-urlencoded";
        requisicao.ContentLength = data.Length;
        requisicao.Proxy = HttpWebRequest.DefaultWebProxy;
        requisicao.Proxy.Credentials = CredentialCache.DefaultCredentials;

        var stream_requisicao = requisicao.GetRequestStream();
        stream_requisicao.Write(data, 0, data.Length);
        stream_requisicao.Close();

        var resposta = requisicao.GetResponse();
        var stream_resposta = resposta.GetResponseStream();
        var leitor = new StreamReader(stream_resposta, Encoding.Default);

        return XDocument.Parse(leitor.ReadToEnd());
    }

    public class Transacao
    {
        public enum Status
        {
            Criada = 0,
            EmAndamento = 1,
            Autenticada = 2,
            NãoAutenticada = 3,
            AutorizadaOuPendenteDeCaptura = 4,
            NãoAutorizada = 5,
            Capturada = 6,
            NãoCapturada = 8,
            Cancelada = 9,
            EmAutenticação = 10
        };

        public string tid { get; set; }

        public string pan { get; set; }

        public string lr { get; set; }

        public string arp { get; set; }

        public string nsu { get; set; }

        public Status status { get; set; }

        public string url_autenticacao { get; set; }

        public string valor { get; set; }

        public string mensagem { get; set; }

        public Transacao(XDocument xml)
        {
            if (xml.Root.LocalElement("tid") != null)
                tid = xml.Root.LocalElement("tid").Value;

            if (xml.Root.LocalElement("dados-pedido") != null)
                if (xml.Root.LocalElement("dados-pedido").LocalElement("valor") != null)
                    valor = xml.Root.LocalElement("dados-pedido").LocalElement("valor").Value;

            if (xml.Root.LocalElement("status") != null)
                status = (Transacao.Status)Enum.Parse(typeof(Transacao.Status), xml.Root.LocalElement("status").Value);

            if (xml.Root.LocalElement("autorizacao") != null)
            {
                if (xml.Root.LocalElement("autorizacao") != null)
                    lr = xml.Root.LocalElement("autorizacao").LocalElement("lr").Value;

                if (xml.Root.LocalElement("autorizacao").LocalElement("arp") != null)
                    arp = xml.Root.LocalElement("autorizacao").LocalElement("arp").Value;

                if (xml.Root.LocalElement("autorizacao").LocalElement("nsu") != null)
                    nsu = xml.Root.LocalElement("autorizacao").LocalElement("nsu").Value;

                if (xml.Root.LocalElement("autorizacao").LocalElement("mensagem") != null)
                    mensagem = xml.Root.LocalElement("autorizacao").LocalElement("mensagem").Value;
            }

            if (xml.Root.LocalElement("url-autenticacao") != null)
                url_autenticacao = xml.Root.LocalElement("url-autenticacao").Value;
        }
    }
}

public static class XMLExtensions
{
    public static XElement LocalElement(this XContainer self, XName name)
    {
        return self.Elements().SingleOrDefault(a => a.Name.LocalName == name);
    }
}
