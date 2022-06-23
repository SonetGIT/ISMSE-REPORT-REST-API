using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Intersoft.CISSA.BizService.Utils;
using Intersoft.CISSA.DataAccessLayer.Core;
using Intersoft.CISSA.DataAccessLayer.Model.Context;
using Intersoft.CISSA.DataAccessLayer.Model.Query.Builders;
using Intersoft.CISSA.DataAccessLayer.Model.Workflow;
using Intersoft.CISSA.DataAccessLayer.Model.Query.Sql;
using Intersoft.CISSA.DataAccessLayer.Model.Data;
using Intersoft.CISSA.DataAccessLayer.Model.Documents;
using Intersoft.CISSA.DataAccessLayer.Repository;

using ASIST_REPORT_REST_API.Models;
using Intersoft.CISSA.DataAccessLayer.Model.Enums;
using Intersoft.CISSA.DataAccessLayer.Model.Query;
using ASIST_REPORT_REST_API.Models.Address;
using System.IO;
using System.Data;
using System.Reflection;
using ASIST_REPORT_REST_API.Models.Report;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace ISMSE_REPORT_REST_API.Util
{
    public static class ScriptExecutor
    {
        public static WorkflowContext CreateAsistContext(string username, Guid userId)
        {
            var dataContextFactory = DataContextFactoryProvider.GetFactory();

            var dataContext = dataContextFactory.CreateMultiDc("asistDataContexts");
            BaseServiceFactory.CreateBaseServiceFactories();
            var providerFactory = AppServiceProviderFactoryProvider.GetFactory();
            var provider = providerFactory.Create(dataContext);
            var serviceRegistrator = provider.Get<IAppServiceProviderRegistrator>();
            serviceRegistrator.AddService(new UserDataProvider(userId, username));
            return new WorkflowContext(new WorkflowContextData(Guid.Empty, userId), provider);
        }
        public static WorkflowContext CreateNrszContext(string username, Guid userId)
        {
            var dataContextFactory = DataContextFactoryProvider.GetFactory();
            var dataContext = dataContextFactory.CreateMultiDc("nrszDataContexts");
            BaseServiceFactory.CreateBaseServiceFactories();
            var providerFactory = AppServiceProviderFactoryProvider.GetFactory();
            var provider = providerFactory.Create(dataContext);
            var serviceRegistrator = provider.Get<IAppServiceProviderRegistrator>();
            serviceRegistrator.AddService(new UserDataProvider(userId, username));
            return new WorkflowContext(new WorkflowContextData(Guid.Empty, userId), provider);
        }

        private static readonly Guid asistPersonDefId = new Guid("{6052978A-1ECB-4F96-A16B-93548936AFC0}");
        static Guid appDefId = new Guid("{4F9F2AE2-7180-4850-A3F4-5FB47313BCC0}");
        public class FamilyDetails
        {
            public Person Applicant { get; set; }
            public FamilyMembersDetail FamilyMembers { get; set; }
            public ASPApplication ASPApplication { get; set; }
        }

        public class FamilyMembersDetail
        {
            public int? all { get; set; }
            public int? group_1 { get; set; }
            public int? children_disabilities { get; set; }
            public int? Children_under_16 { get; set; }
            public int? Older_than_65 { get; set; }
        }
        public class ASPApplication
        {
            public string No { get; set; }
            public DateTime? Date { get; set; }
            public DateTime? ApplicationDate { get; set; }
            public Person Trustee { get; set; }
            public int OblastNo { get; set; }
            public string OblastName { get; set; }
            public int RaionNo { get; set; }
            public string RaionName { get; set; }
            public string Djamoat { get; set; }
            public string Village { get; set; }
            public string Street { get; set; }
            public string House { get; set; }
            public string Flat { get; set; }
            public string DisabilityGroup { get; set; }
        }
        public class Person
        {
            public string IIN { get; set; }
            public string SIN { get; set; }
            public string Last_Name { get; set; }
            public string First_Name { get; set; }
            public string Middle_Name { get; set; }
            public Guid? GenderId { get; set; }
            public string GenderText { get; set; }
            public DateTime? Date_of_Birth { get; set; }
            public Guid? PassportTypeId { get; set; }
            public string PassportTypeText { get; set; }
            public string PassportSeries { get; set; }
            public string PassportNo { get; set; }
            public DateTime? Date_of_Issue { get; set; }
            public string Issuing_Authority { get; set; }
        }
        public static FamilyDetails GetFamilyDetailsByIIN(string applicantIIN)
        {
            var context = CreateAsistContext("sod_user", new Guid("{4296EF4D-ED7A-41F8-BE88-2684AD21AC0E}"));

            var qb = new QueryBuilder(appDefId);
            qb.Where("Person").Include("IIN").Eq(applicantIIN).End();
            var query = context.CreateSqlQuery(qb);
            var ui = context.GetUserInfo();
            query.AddAttribute("&Id");
            query.AddOrderAttribute("&Created", false);
            var id = Guid.Empty;
            using (var reader = context.CreateSqlReader(query))
            {
                if (reader.Read())
                {
                    id = reader.GetGuid(0);
                }
            }
            if (id != Guid.Empty)
            {
                var model = new FamilyDetails();
                var docRepo = context.Documents;
                var app = docRepo.LoadById(id);
                var person = docRepo.LoadById((Guid)app["Person"]);

                model.Applicant = InitPerson(context, person);
                model.ASPApplication = new ASPApplication
                {
                    ApplicationDate = (DateTime?)app["ApplicationDate"],
                    Date = (DateTime?)app["Date"],
                    No = app["No"] != null ? app["No"].ToString() : "",
                    Trustee = app["Trustee"] != null ? InitPerson(context, docRepo.LoadById((Guid)app["Trustee"])) : null
                };
                if (app["Application_State"] != null)
                {
                    var appState = docRepo.LoadById((Guid)app["Application_State"]);
                    model.FamilyMembers = new FamilyMembersDetail
                    {
                        all = (int?)appState["all"],
                        children_disabilities = (int?)appState["children_disabilities"],
                        Children_under_16 = (int?)appState["Children_under_16"],
                        group_1 = (int?)appState["group_1"],
                        Older_than_65 = (int?)appState["Older_than_65"]
                    };
                    if (appState["Disability"] != null)
                    {
                        model.ASPApplication.DisabilityGroup = context.Enums.GetValue((Guid)appState["Disability"]).Value;
                        if (appState["DisabilityGroupe"] != null)
                        {
                            model.ASPApplication.DisabilityGroup += ", " + context.Enums.GetValue((Guid)appState["DisabilityGroupe"]).Value;
                        }
                    }
                    if (appState["RegionId"] != null)
                    {
                        var regionObj = docRepo.LoadById((Guid)appState["RegionId"]);
                        model.ASPApplication.OblastNo = (int?)regionObj["Number"] ?? 0;
                        model.ASPApplication.OblastName = regionObj["Name"] != null ? regionObj["Name"].ToString() : "";
                    }
                    if (appState["DistrictId"] != null)
                    {
                        var districtObj = docRepo.LoadById((Guid)appState["DistrictId"]);
                        model.ASPApplication.RaionNo = (int?)districtObj["Number"] ?? 0;
                        model.ASPApplication.RaionName = districtObj["Name"] != null ? districtObj["Name"].ToString() : "";
                    }
                    if (appState["DjamoatId"] != null)
                    {
                        var djamoatObj = docRepo.LoadById((Guid)appState["DjamoatId"]);
                        model.ASPApplication.Djamoat = djamoatObj["Name"] != null ? djamoatObj["Name"].ToString() : "";
                    }
                    if (appState["VillageId"] != null)
                    {
                        var villageObj = docRepo.LoadById((Guid)appState["VillageId"]);
                        model.ASPApplication.Village = villageObj["Name"] != null ? villageObj["Name"].ToString() : "";
                    }
                    if (appState["street"] != null)
                    {
                        model.ASPApplication.Street = appState["street"].ToString();
                    }
                    if (appState["House"] != null)
                    {
                        model.ASPApplication.House = appState["House"].ToString();
                    }
                    if (appState["flat"] != null)
                    {
                        model.ASPApplication.Flat = appState["flat"].ToString();
                    }
                }
                return model;
            }
            return null;
        }
        public static FamilyDetails GetFamilyDetailsBySIN(string applicantSIN)
        {
            var context = CreateAsistContext("sod_user", new Guid("{4296EF4D-ED7A-41F8-BE88-2684AD21AC0E}"));

            var qb = new QueryBuilder(appDefId, context.UserId);
            qb.Where("Person").Include("SIN").Eq(applicantSIN).End();
            var query = context.CreateSqlQuery(qb);
            query.AddAttribute("&Id");
            query.AddOrderAttribute("&Created", false);
            var id = Guid.Empty;
            using (var reader = context.CreateSqlReader(query))
            {
                if (reader.Read())
                {
                    id = reader.GetGuid(0);
                }
            }
            if (id != Guid.Empty)
            {
                var model = new FamilyDetails();
                var docRepo = context.Documents;
                var app = docRepo.LoadById(id);
                var person = docRepo.LoadById((Guid)app["Person"]);

                model.Applicant = InitPerson(context, person);
                model.ASPApplication = new ASPApplication
                {
                    ApplicationDate = (DateTime?)app["ApplicationDate"],
                    Date = (DateTime?)app["Date"],
                    No = app["No"] != null ? app["No"].ToString() : "",
                    Trustee = app["Trustee"] != null ? InitPerson(context, docRepo.LoadById((Guid)app["Trustee"])) : null
                };
                if (app["Application_State"] != null)
                {
                    var appState = docRepo.LoadById((Guid)app["Application_State"]);
                    model.FamilyMembers = new FamilyMembersDetail
                    {
                        all = (int?)appState["all"],
                        children_disabilities = (int?)appState["children_disabilities"],
                        Children_under_16 = (int?)appState["Children_under_16"],
                        group_1 = (int?)appState["group_1"],
                        Older_than_65 = (int?)appState["Older_than_65"]
                    };
                }

                return model;
            }
            return null;
        }
        static Person InitPerson(WorkflowContext context, Doc person)
        {
            var p = new Person();
            p.IIN = person["IIN"] != null ? person["IIN"].ToString() : "";
            p.SIN = person["SIN"] != null ? person["SIN"].ToString() : "";
            p.Last_Name = person["Last_Name"] != null ? person["Last_Name"].ToString() : "";
            p.First_Name = person["First_Name"] != null ? person["First_Name"].ToString() : "";
            p.Middle_Name = person["Middle_Name"] != null ? person["Middle_Name"].ToString() : "";
            p.GenderId = person["Sex"] != null ? (Guid)person["Sex"] : Guid.Empty;
            p.GenderText = person["Sex"] != null ? context.Enums.GetValue((Guid)person["Sex"]).Value : "";
            p.Date_of_Birth = (DateTime?)person["Date_of_Birth"];
            p.PassportTypeId = person["PassportType"] != null ? (Guid)person["PassportType"] : Guid.Empty;
            p.PassportTypeText = person["PassportType"] != null ? context.Enums.GetValue((Guid)person["PassportType"]).Value : "";
            p.PassportSeries = person["PassportSeries"] != null ? person["PassportSeries"].ToString() : "";
            p.PassportNo = person["PassportNo"] != null ? person["PassportNo"].ToString() : "";
            p.Date_of_Issue = (DateTime?)person["Date_of_Issue"];
            p.Issuing_Authority = person["Issuing_Authority"] != null ? person["Issuing_Authority"].ToString() : "";
            return p;
        }
        public static void SetAssigned(string applicantIIN)
        {
            var context = CreateAsistContext("sod_user", new Guid("{4296EF4D-ED7A-41F8-BE88-2684AD21AC0E}"));

            var qb = new QueryBuilder(appDefId, context.UserId);
            qb.Where("Person").Include("IIN").Eq(applicantIIN).End();
            var query = context.CreateSqlQuery(qb);
            query.AddAttribute("&Id");
            query.AddOrderAttribute("&Created", false);
            var id = Guid.Empty;
            using (var reader = context.CreateSqlReader(query))
            {
                if (reader.Read())
                {
                    id = reader.GetGuid(0);
                }
            }
            if (id != Guid.Empty)
            {
                var docRepo = context.Documents;
                var app = docRepo.LoadById(id);
                var person = docRepo.LoadById((Guid)app["Person"]);
                app["RitualBenefitAssigned"] = true;
                docRepo.Save(app);
            }
        }
        private static readonly Guid NrszPersonDefId = new Guid("{6052978A-1ECB-4F96-A16B-93548936AFC0}");
        public class AssignServiceRequest
        {
            public string pin { get; set; }
            public DateTime effectiveDate { get; set; }
            public DateTime expiryDate { get; set; }
            public Guid serviceTypeId { get; set; }
            public double amount { get; set; }
            public int oblastNo { get; set; }
            public int raionNo { get; set; }
            public string djamoat { get; set; }
            public string village { get; set; }
            public string street { get; set; }
            public string house { get; set; }
            public string flat { get; set; }
            public string disabilityGroup { get; set; }
        }
        public static void AssignService(AssignServiceRequest request)
        {
            var nrszContext = CreateNrszContext("asist2nrsz", new Guid("{05EEF54F-5BFE-4E2B-82C7-6AB6CD59D488}"));
            nrszContext.DataContext.BeginTransaction();
            try
            {
                var qb = new QueryBuilder(NrszPersonDefId);
                qb.Where("IIN").Eq(request.pin);
                Guid personId;
                Guid districtId;
                Guid regionId;

                using (var query = nrszContext.CreateSqlQuery(qb.Def))
                {
                    query.AddAttribute("&Id");
                    using (var reader = nrszContext.CreateSqlReader(query))
                    {
                        if (!reader.Read())
                            throw new ApplicationException(
                                String.Format("Не могу зарегистрированить назначение. Гражданин с указанным ПИН \"{0}\" не найден!", request.pin));

                        personId = reader.Reader.GetGuid(0);
                    }
                }
                qb = new QueryBuilder(RaionDefId);
                qb.Where("Number").Eq(request.raionNo).And("Area").Include("Number").Eq(request.oblastNo);

                using (var query = nrszContext.CreateSqlQuery(qb.Def))
                {
                    query.AddAttribute("&Id");
                    query.AddAttribute(query.Sources[0], "&Id");
                    using (var reader = nrszContext.CreateSqlReader(query))
                    {
                        if (!reader.Read())
                            throw new ApplicationException("Не могу зарегистрировать назначение. Ошибка в коде области или района!");

                        districtId = reader.Reader.GetGuid(0);
                        regionId = reader.Reader.GetGuid(1);
                    }
                }
                var docRepo = nrszContext.Documents;
                var assignedService = docRepo.New(AssignedServiceDefId);
                assignedService["Person"] = personId;
                assignedService["RegDate"] = DateTime.Now;
                assignedService["DateFrom"] = request.effectiveDate;
                assignedService["DateTo"] = request.expiryDate;
                if (request.amount > 0)
                    assignedService["Amount"] = request.amount;
                assignedService["ServiceType"] = request.serviceTypeId;
                var userInfo = nrszContext.GetUserInfo();
                assignedService["AuthorityId"] = userInfo.OrganizationId;
                assignedService["District"] = districtId;
                assignedService["Area"] = regionId;
                assignedService["Djamoat"] = request.djamoat;
                assignedService["Village"] = request.village;
                assignedService["Street"] = request.street;
                assignedService["House"] = request.house;
                assignedService["Flat"] = request.flat;

                assignedService["DisabilityGroup"] = request.disabilityGroup;


                nrszContext.Documents.Save(assignedService);
                nrszContext.DataContext.Commit();
            }
            catch (Exception e)
            {
                nrszContext.DataContext.Rollback();
                throw e;
            }
        }

        public static readonly Guid ServiceTypeEnumDefId = new Guid("{EA5A7FC9-19AF-4E18-BF21-E8EE29D585C7}");
        public static readonly Guid TsaBenefitEnumId = new Guid("{C5C95DC9-CEFE-46F5-B6AA-4D23E5CE1008}"); // Пособие на АСП
        public static readonly Guid TsaPoorStatusEnumId = new Guid("{371B3E58-C039-4F8C-A299-B62666C23AB6}"); // Статус малообеспеченной семьи
        public static readonly Guid TsaDeadBenefitEnumId = new Guid("{2A5FA716-7D3A-467B-B7C8-C1E68251E7D4}"); // Пособие на погребение
        public static readonly Guid AssignedServiceDefId = new Guid("{A16EE2A1-CFDF-4B7A-8A32-28CC094C3486}");
        private static readonly Guid RaionDefId = new Guid("{BA5D4276-6BFB-4180-9D4F-828E38E95601}");
        private static readonly Guid AssignedServicePaymentDefId = new Guid("{B9CB0BD2-9BD5-4F91-AD12-94B9FA6FC21D}");

        static void WriteLog(object text)
        {
            using (StreamWriter sw = new StreamWriter("c:\\distr\\cissa\\asist-rest-api.log", true))
            {
                sw.WriteLine(text.ToString());
            }
        }

        //ОТЧЕТНОСТЬ ИСМСЭ
        //Результат первичного освидетельствования
        public static class Report01
        {
            public static List<Report01Item> Execute(WorkflowContext context, int year, string distrSpr)
            {
                var ui = context.GetUserInfo();
                var orgId = ui.OrganizationId.Value;

                /******************************************************************************************************************/
                var docs = new string[]
                {
                    "освидетельствовано всего  01  ",
                    "в т. ч. для определения группы инвалидности  02  ",
                    "из них: не признаны   ЛОВЗ	 03  ",
                    "признаны временно нетрудоспособными  04  ",
                    "признаны    ЛОВЗ  05  ",
                    "из числа признанных ЛОВЗ (ст 05)  ЛОВЗ   1 группы  06  ",
                    "ЛОВЗ     2 группы  07  ",
                    "ЛОВЗ     3 группы  08  ",
                    "из числа признанных ЛОВЗ (ст 05) ЛОВЗ вследствие трудового увечья  09  ",
                    "ЛОВЗ вследствие профессионального заболевания  10  ",
                    "ЛОВЗ вследствие общего заболевания  11  ",
                    "в том числе: ЛОВЗ – участники ВОВ  11.1",
                    "ЛОВЗ -  труженики тыла  11.2",
                    "ЛОВЗ   «с детства»  12  ",
                    "ЛОВЗ из числа военнослужащих  13  ",
                    "из них: ЛОВЗ   участники ВОВ  13.1",
                    "ЛОВЗ, вследствие ранений, контузий, травм, увечий, заболеваний, полученных при исполнении обязанностей военной службы  13.2",
                    "ЛОВЗ    вследствие заболеваний, полученных в период   прохождений военной службы  13.3",
                    "ЛОВЗ     вследствие ликвидации аварии ЧАЭС 14  ",
                    "при выполнении обязанностей в/службы  14.1",
                    "из числа признанных ЛОВЗ (ст. 05) инвалидность установлена бессрочно  15  ",
                    "освидетельствование для других целей  16  ",
                    "в том числе предоставление кресла-коляски  16.1",
                    "определение в дом инвалидов  16.2",
                    "установление % утрат проф. трудоспособности  16.3",
                    "для сложных консультаций  16.4"
                };
                var items = new List<Report01Item>();
                int i2 = 1;
                foreach (var i in docs)
                {
                    var item = new Report01Item();
                    item.rowName = i;
                    item.rowNo = i2;
                    i2++;
                    items.Add(item);
                }
                CalcItems(context, /*report, */year, orgId, distrSpr, items[25],
                    items[24], items[23], items[22], items[21], items[20], items[19], items[18],
                    items[17], items[16], items[15], items[14], items[13], items[12], items[11],
                    items[10], items[9], items[8], items[7], items[6], items[5], items[4],
                    items[3], items[2], items[1], items[0]);
                return items;
            }
            public static void CalcItems(WorkflowContext context, int year, Guid orgId, string distrSpr,
                Report01Item item25, Report01Item item24,
                Report01Item item23, Report01Item item22, Report01Item item21, Report01Item item20,
                Report01Item item19, Report01Item item18, Report01Item item17, Report01Item item16,
                Report01Item item15, Report01Item item14, Report01Item item13, Report01Item item12,
                Report01Item item11, Report01Item item10, Report01Item item9, Report01Item item8,
                Report01Item item7, Report01Item item6, Report01Item item5, Report01Item item4,
                Report01Item item3, Report01Item item2, Report01Item item1, Report01Item item0)
            {
                var qb = new QueryBuilder(adultsStatCardDefId, context.UserId);
                qb.Where("&State").Eq(ApprovedStateId).And("Examination1").Eq(primaryEnumId);

                using (var query = SqlQueryBuilder.Build(context.DataContext, qb.Def))
                {
                    var personSrc = query.JoinSource(query.Source, personDefId, SqlSourceJoinType.Inner, "Person");
                    var actSrc = query.JoinSource(query.Source, adultsActDefId, SqlSourceJoinType.Inner, "AdultsMedicalCertificate");
                    var distrSrc = query.JoinSource(actSrc, districtDefId, SqlSourceJoinType.LeftOuter, "DistrictId");
                    var citySrc = query.JoinSource(actSrc, cityDefId, SqlSourceJoinType.LeftOuter, "CityId");
                    query.AddCondition(ExpressionOperation.And, adultsStatCardDefId, "&OrgId", ConditionOperation.Equal, orgId);
                    if (!string.IsNullOrEmpty(distrSpr))
                        query.AddCondition(ExpressionOperation.And, districtDefId, "NumberDis", ConditionOperation.Equal, distrSpr);

                    query.AddAttribute(query.Source, "&Id");
                    query.AddAttribute(query.Source, "EndDateExamination");
                    query.AddAttribute(personSrc, "Gender");
                    query.AddAttribute(query.Source, "AgeOf");
                    query.AddAttribute(query.Source, "PlaceWork");
                    query.AddAttribute(query.Source, "GoalSurvey");
                    query.AddAttribute(query.Source, "DisabilityGroup");
                    query.AddAttribute(query.Source, "CauseOfDisability");
                    query.AddAttribute(query.Source, "Indefinitely");
                    query.AddAttribute(query.Source, "goal1");
                    query.AddAttribute(query.Source, "goal2");
                    query.AddAttribute(query.Source, "goal3");
                    query.AddAttribute(query.Source, "goal4");
                    query.AddAttribute(query.Source, "goal5");
                    query.AddAttribute(query.Source, "goal6");
                    query.AddAttribute(query.Source, "goal7");
                    query.AddAttribute(query.Source, "goal8");
                    query.AddAttribute(query.Source, "goal9");
                    query.AddAttribute(query.Source, "goal11");
                    query.AddAttribute(query.Source, "goal12");
                    query.AddAttribute(query.Source, "goal13");
                    query.AddAttribute(query.Source, "goal14");
                    query.AddAttribute(query.Source, "goal15");
                    query.AddAttribute(query.Source, "goal16");
                    query.AddAttribute(query.Source, "goal17");
                    query.AddAttribute(query.Source, "goal18");
                    query.AddAttribute(distrSrc, "DistrictType");
                    query.AddAttribute(citySrc, "DistrictType");

                    var table = new DataTable();
                    using (var reader = context.CreateSqlReader(query))
                    {
                        reader.Open();
                        reader.Fill(table);
                        reader.Close();
                    }

                    foreach (DataRow row in table.Rows)
                    {
                        var appId = row[0] is DBNull ? Guid.Empty : (Guid)row[0];
                        var date = !(row[1] is DBNull) ? (DateTime)row[1] : DateTime.MinValue;
                        var gender = row[2] is DBNull ? Guid.Empty : (Guid)row[2];
                        var ageOf = row[3] is DBNull ? Guid.Empty : (Guid)row[3];
                        var busy = row[4] is DBNull ? "" : row[4].ToString();
                        var goal = row[5] is DBNull ? Guid.Empty : (Guid)row[5];
                        var group = row[6] is DBNull ? Guid.Empty : (Guid)row[6];
                        var reason = row[7] is DBNull ? Guid.Empty : (Guid)row[7];
                        var indef = row[8] is DBNull ? false : (bool)row[8];
                        var goal1 = row[9] is DBNull ? false : (bool)row[9];
                        var goal2 = row[10] is DBNull ? false : (bool)row[10];
                        var goal3 = row[11] is DBNull ? false : (bool)row[11];
                        var goal4 = row[12] is DBNull ? false : (bool)row[12];
                        var goal5 = row[13] is DBNull ? false : (bool)row[13];
                        var goal6 = row[14] is DBNull ? false : (bool)row[14];
                        var goal7 = row[15] is DBNull ? false : (bool)row[15];
                        var goal8 = row[16] is DBNull ? false : (bool)row[16];
                        var goal9 = row[17] is DBNull ? Guid.Empty : (Guid)row[17];
                        var goal11 = row[18] is DBNull ? false : (bool)row[18];
                        var goal12 = row[19] is DBNull ? false : (bool)row[19];
                        var goal13 = row[20] is DBNull ? false : (bool)row[20];
                        var goal14 = row[21] is DBNull ? false : (bool)row[21];
                        var goal15 = row[22] is DBNull ? false : (bool)row[22];
                        var goal16 = row[23] is DBNull ? false : (bool)row[23];
                        var goal17 = row[24] is DBNull ? false : (bool)row[24];
                        var goal18 = row[25] is DBNull ? false : (bool)row[25];
                        var district = row[26] is DBNull ? Guid.Empty : (Guid)row[26];
                        var city = row[27] is DBNull ? Guid.Empty : (Guid)row[27];

                        if (appId != Guid.Empty)
                        {
                            if (date.Year == year)
                            {
                                //item0.gr1 = item0.gr1 != 0 ? (1 + (int)item0.gr1) : 1;
                                if (gender == womanEnumId) item0.gr2 = item0.gr2 != 0 ? (1 + (int)item0.gr2) : 1;
                                if (ageOf == ageOf1EnumId) item0.gr3 = item0.gr3 != 0 ? (1 + (int)item0.gr3) : 1;
                                if (ageOf == ageOf2EnumId) item0.gr4 = item0.gr4 != 0 ? (1 + (int)item0.gr4) : 1;
                                if (ageOf == ageOf3EnumId) item0.gr5 = item0.gr5 != 0 ? (1 + (int)item0.gr5) : 1;
                                if (ageOf == ageOf4EnumId) item0.gr6 = item0.gr6 != 0 ? (1 + (int)item0.gr6) : 1;
                                {
                                    if (gender == womanEnumId) item0.gr7 = item0.gr7 != 0 ? (1 + (int)item0.gr7) : 1;
                                }
                                if (district == cityEnumId || city == cityEnumId)
                                {
                                    item0.gr8 = item0.gr8 != 0 ? (1 + (int)item0.gr8) : 1;
                                    if (gender == womanEnumId) item0.gr9 = item0.gr9 != 0 ? (1 + (int)item0.gr9) : 1;
                                    if (ageOf == ageOf1EnumId || ageOf == ageOf2EnumId || ageOf == ageOf3EnumId)
                                        item0.gr10 = item0.gr10 != 0 ? (1 + (int)item0.gr10) : 1;
                                    if (busy != null) item0.gr11 = item0.gr11 != 0 ? (1 + (int)item0.gr11) : 1;
                                }
                                if (district == villageEnumId || city == villageEnumId)
                                {
                                    item0.gr12 = item0.gr12 != 0 ? (1 + (int)item0.gr12) : 1;
                                    if (gender == womanEnumId) item0.gr13 = item0.gr13 != 0 ? (1 + (int)item0.gr13) : 1;
                                    if (ageOf == ageOf1EnumId || ageOf == ageOf2EnumId || ageOf == ageOf3EnumId)
                                        item0.gr14 = item0.gr14 != 0 ? (1 + (int)item0.gr14) : 1;
                                    if (busy != null) item0.gr15 = item0.gr15 != 0 ? (1 + (int)item0.gr15) : 1;
                                }
                            }
                        }
                    }
                }
            }
            private static readonly Guid reportDefId = new Guid("{17789FEE-2908-4E01-8E16-2ECAF4A75C7A}"); //Отчет о распределение признанных ЛОВЗ по формам болезней, возрасту и группам инвалидности
            private static readonly Guid reportItemDefId = new Guid("{10AB1374-EA9C-493B-B5F6-CD6A40273B9B}"); //Распределение признанных ЛОВЗ по формам болезней, возрасту и группам инвалидности

            private static readonly Guid adultsStatCardDefId = new Guid("{C77A6FF6-EC0B-4562-81FE-BB3BC17F4FDB}"); //Статистический талон для взрослых
            private static readonly Guid adultsActDefId = new Guid("{12AE380B-39CE-4753-A556-4083BD4B0F92}"); //Медицинский акт для взрослых
            private static readonly Guid personDefId = new Guid("{D71CE61A-9B59-4B5E-8713-8131DBB5BA02}"); //Person 
            public static readonly Guid districtDefId = new Guid("{A3FCA356-82A9-4BBD-872A-8333BEC6E41A}"); //Район
            public static readonly Guid cityDefId = new Guid("{4BB6D32D-5181-4031-BA49-CF5910D6D883}"); //Города районного значения

            public static readonly Guid goalForGroup = new Guid("{8FD84B27-FF2A-40A4-A87A-BC254DC9D853}"); //Цель -> Для установления группы

            private static readonly Guid[] groupInvalId = new[]
            {
                new Guid("{D145ABA1-EADA-442B-9D8D-AD10BCD126D2}"),
                new Guid("{C161DA2D-2883-4211-B444-CBB04DBF33D5}"),
                new Guid("{CCD439CA-1500-4F7A-A1C8-BB474B5DE8AA}")
            };
            //причины инвалидности
            private static readonly Guid reasonDisEnumdId1 = new Guid("{3A8053A5-CD06-49D3-A551-2247655B13C3}"); //	Общее заболевание
            private static readonly Guid reasonDisEnumdId2 = new Guid("{9AF55221-0264-4349-9B36-A8E1313250B6}"); //	ЛОВЗ с детства
            private class Report01Item
            {
                public Guid RegionId;
                public Guid DistrictId;
            }
        }
    }
}




