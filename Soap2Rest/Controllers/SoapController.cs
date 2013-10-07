using System.IO;
using System.Web.Mvc;
using System.Web.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;

namespace Soap2Rest.Controllers
{
    public class SoapController : ApiController
    {
        [OutputCache(Duration = 60 * 1, VaryByParam = "action;url;payload", Location = OutputCacheLocation.ServerAndClient, NoStore = true)]
        public async Task<HttpResponseMessage> Get(string action, string service, string payload)
        {
            const string soapTemplate =
                "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body></soap:Body></soap:Envelope>";
            var requestXml = XDocument.Parse(soapTemplate);
            var requestEnvelope = requestXml.Root;
            if (requestEnvelope == null)
                throw new InvalidOperationException("Invalid Xml SoapTemplate");
            var requestBody = requestEnvelope.Descendants().Single();
            var localName = Path.GetFileName(action);
            var namespaceName = action.Substring(0, action.Length - localName.Length);
            XName tag = XName.Get(localName, namespaceName);
            if (!string.IsNullOrEmpty(payload) && payload != "*")
            {
                var requestPayload = XElement.Parse(payload);
                tag = requestPayload.Name;
                requestBody.Add(requestPayload);
            }
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("SOAPTARGET", service);
            httpClient.DefaultRequestHeaders.Add("SOAPAction", action);
            var request = new StringContent(requestXml.ToString(SaveOptions.DisableFormatting), Encoding.UTF8,
                                            "text/xml");
            HttpResponseMessage resultResponse = await httpClient.PostAsync(service, request);
            var resultContent = await resultResponse.Content.ReadAsStringAsync();
            var resultXml = XDocument.Parse(resultContent);
            if (resultXml.Root == null)
                throw new InvalidOperationException("Invalid Response: " + resultContent);
            var tagResult = tag.Namespace.GetName(tag.LocalName + "Response");
            var resultBody = resultXml.Root.Descendants(tagResult).Single();
            if (resultBody == null)
                throw new InvalidOperationException("Invalid ResultBody: " + resultXml.Root.ToString());

            string content = "";
            string mediaType = "text/plain";
            var accept = Request.Headers.Accept;
            if (accept.Contains(MediaTypeWithQualityHeaderValue.Parse("text/xml")))
            {
                    content = resultBody.ToString(SaveOptions.OmitDuplicateNamespaces);
                    mediaType = "text/xml";
            }
            if (accept.Contains(MediaTypeWithQualityHeaderValue.Parse("application/json")))
            {
                   content = JsonConvert.SerializeXNode(resultBody, Formatting.Indented, true);
                    mediaType = "application/json";
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            return response;
        }
    }
}
