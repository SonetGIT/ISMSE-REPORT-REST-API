using ASIST_REPORT_REST_API.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using System.IO;
using Intersoft.CISSA.DataAccessLayer.Model.Workflow;
using Intersoft.CISSA.DataAccessLayer.Model.Context;

namespace ASIST_REPORT_REST_API.Controllers
{
    public class DocumentController : ApiController
    {
        private sodEntities _sod = new sodEntities();
        [HttpGet]
        [ResponseType(typeof(ScriptExecutor.FamilyDetails))]
        public IHttpActionResult GetFamilyDetailsByIIN(string applicantIIN)
        {
            var log = new RequestLog
            {
                ConnectionName = "GetFamilyDetailsByIIN",
                RequestDate = DateTime.Now,
                Result = "OK"
            };
            try
            {
                var result = ScriptExecutor.GetFamilyDetailsByIIN(applicantIIN);
                if (result == null)
                {
                    log.Result = "Гражданин не найден - " + applicantIIN;
                    return Ok(new { result = false, error = "Гражданин не найден - " + applicantIIN });
                }
                return Ok(new { result = true, data = result });
            }
            catch (Exception e)
            {
                log.Result = "Error: " + e.GetBaseException().Message;
                return Ok(new { result = false, error = e.GetBaseException().Message });
            }
            finally
            {
                log.EllapsedTime = (int)Math.Round((DateTime.Now - log.RequestDate.Value).TotalMilliseconds, 0);
                _sod.RequestLogs.Add(log);
                _sod.SaveChanges();
            }
        }
        [HttpGet]
        [ResponseType(typeof(ScriptExecutor.FamilyDetails))]
        public IHttpActionResult GetFamilyDetailsByIINWithAssign(string applicantIIN)
        {
            var log = new RequestLog
            {
                ConnectionName = "GetFamilyDetailsByIIN",
                RequestDate = DateTime.Now,
                Result = "OK"
            };
            try
            {
                var result = ScriptExecutor.GetFamilyDetailsByIIN(applicantIIN);
                if (result == null)
                {
                    log.Result = "Гражданин не найден - " + applicantIIN;
                    return Ok(new { result = false, error = "Гражданин не найден - " + applicantIIN });
                }
                ScriptExecutor.AssignService(new ScriptExecutor.AssignServiceRequest
                {
                    pin = applicantIIN,
                    serviceTypeId = new Guid("{6EA2082A-D3E9-49F0-836F-F5BE775251BD}"),
                    disabilityGroup = "",
                    amount = 0,
                    djamoat = "",
                    raionNo = 0,
                    oblastNo = 0
                });
                return Ok(new { result = true, data = result });
            }
            catch (Exception e)
            {
                log.Result = "Error: " + e.GetBaseException().Message;
                return Ok(new { result = false, error = e.GetBaseException().Message });
            }
            finally
            {
                log.EllapsedTime = (int)Math.Round((DateTime.Now - log.RequestDate.Value).TotalMilliseconds, 0);
                _sod.RequestLogs.Add(log);
                _sod.SaveChanges();
            }
        }

        [HttpGet]
        [ResponseType(typeof(ScriptExecutor.FamilyDetails))]
        public IHttpActionResult GetFamilyDetailsBySIN(string applicantSIN)
        {
            var log = new RequestLog
            {
                ConnectionName = "GetFamilyDetailsBySIN",
                RequestDate = DateTime.Now,
                Result = "OK"
            };
            try
            {
                var result = ScriptExecutor.GetFamilyDetailsBySIN(applicantSIN);
                if (result == null) log.Result = "Гражданин не найден - " + applicantSIN;
                return Ok(new { result = result != null, error = "Гражданин не найден - " + applicantSIN });
            }
            catch (Exception e)
            {
                log.Result = "Error: " + e.GetBaseException().Message;
                return Ok(new { result = false, error = e.GetBaseException().Message });
            }
            finally
            {
                log.EllapsedTime = (int)Math.Round((DateTime.Now - log.RequestDate.Value).TotalMilliseconds, 0);
                _sod.RequestLogs.Add(log);
                _sod.SaveChanges();
            }
        }

        [HttpPost]
        public IHttpActionResult AssignService([FromBody]ScriptExecutor.AssignServiceRequest request)
        {
            try
            {
                ScriptExecutor.AssignService(request);
                return Ok(new { success = true });
            }
            catch (Exception e)
            {
                return Ok(new { success = false, error = e.GetBaseException().Message });
            }
        }

        static void WriteLog(object text)
        {
            using (StreamWriter sw = new StreamWriter("c:\\distr\\cissa\\asist-rest-api.log", true))
            {
                sw.WriteLine(text.ToString());
            }
        }

        [HttpGet]
        public IHttpActionResult Report01([FromUri] int year)
        {
            try
            {
                return Ok(ScriptExecutor.Report01.Execute(year));
            }
            catch (Exception e)
            {
                return BadRequest(e.GetBaseException().Message);
            }
        }
    }
}
