﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TownComparisons.Domain.Abstract;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Script.Serialization;
using TownComparisons.Domain.WebServices.Models;
using TownComparisons.Domain.Entities;
using TownComparisons.Domain.Models;

namespace TownComparisons.Domain.WebServices
{
    /// <summary>
    /// Set of functions that calls KoladaAPI and returns different type of data
    /// </summary>
    public class KoladaTownWebService : TownWebServiceBase
    {
        private static readonly string BaseUrl = "http://api.kolada.se/v2/";

        public override string GetName()
        {
            return "Kolada"; 
        }
        
        public override OrganisationalUnit GetOrganisationalUnit(string id)
        {
            var rawJson = string.Empty;

            var apiRequest = "ou/" + id;
            rawJson = RawJson(BaseUrl + apiRequest);
            var ou = JsonConvert.DeserializeObject<OUs>(rawJson).Values;
            OU theOu = ou.First();

            return new OrganisationalUnit(this.GetName(), theOu.Id, theOu.Title);
        }
        
        public override List<PropertyQueryWithResults> GetPropertyResults(List<PropertyQueryInfo> queries, List<OrganisationalUnitInfo> organisationalUnits) //List<PropertyQuery> queries, List<OrganisationalUnit> organisationalUnits)
        {
            var rawJson = string.Empty;

            //Set the Kolada url
            var apiRequest = "oudata/kpi/";
            apiRequest += string.Join(",", queries.Select(q => q.QueryId).ToList());
            apiRequest += "/ou/" + string.Join(",", organisationalUnits.Select(o => o.OrganisationalUnitId).ToList());
            
            //Load the data from Kolada
            rawJson = RawJson(BaseUrl + apiRequest);

            //serialize the json data
            var kpiAnswers = JsonConvert.DeserializeObject<KpiAnswers>(rawJson).Values;

            //create correct models
            List<PropertyQueryWithResults> results = new List<PropertyQueryWithResults>();
            foreach(PropertyQueryInfo query in queries)
            {
                PropertyQueryWithResults queryWithResults = new PropertyQueryWithResults(query);
                foreach (OrganisationalUnitInfo ou in organisationalUnits)
                {
                    queryWithResults.Results.Add(new PropertyQueryResult(ou.OrganisationalUnitId, kpiAnswers.Where(a => a.Kpi == query.QueryId && a.Ou == ou.OrganisationalUnitId)
                                                                                                                .Select(a => new PropertyQueryResultForPeriod(a.Period, 
                                                                                                                                                                        a.Values.Select(v => new PropertyQueryResultValue(v.Gender, v.Status, v.Value)).ToList()))
                                                                                                                .ToList(), query.Period));
                }
                results.Add(queryWithResults);
            }

            return results;
        }

        public override List<PropertyQueryGroup> GetAllPropertyQueries()
        {
            var rawJson = string.Empty;
            var apiRequest= "kpi_groups";
            rawJson = RawJson(BaseUrl + apiRequest);
            var kpi = JsonConvert.DeserializeObject<KpiGroups>(rawJson).Values;

            return kpi.Select(k => new PropertyQueryGroup(this.GetName(), k.Id, k.Title, k.Members.Select(m => new PropertyQuery(this.GetName(), m.Member_id, m.Member_title, GuessPropertyQueryType(m.Member_title))).ToList())).ToList();
        }
        private string GuessPropertyQueryType(string title)
        {
            if (title.ToLower().Contains("(%)"))
            {
                return PropertyQuery.TYPE_PERCENT;
            }
            else if (title.ToLower().Contains("procentenheter"))
            {
                return PropertyQuery.TYPE_PERCENTAGE;
            }
            else if (title.ToLower().Contains("ja=1") && title.ToLower().Contains("nej=0")) 
            {
                return PropertyQuery.TYPE_YESNO;
            }

            return PropertyQuery.TYPE_STANDARD;
        }

        public override List<OrganisationalUnit> GetAllOrganisationalUnits(string municipalityId)
        {
            var rawJson = string.Empty;
            var apiRequest = "ou?municipality=" + municipalityId;

            rawJson = RawJson(BaseUrl + apiRequest);

            var OUs = JsonConvert.DeserializeObject<OUs>(rawJson).Values;
            return OUs.Select(o => new OrganisationalUnit(this.GetName(), o.Id, o.Title)).ToList();
        }

        /// <summary>
        /// Function:
        /// 1. Takes apiRequest as argument, and builds valid URI
        /// 2. Makes HttpWebRequest
        /// 3. Reads data from request and sends it back in raw format...
        /// 
        /// </summary>
        /// <param name="apiRequest"></param>
        /// <returns>rawJson, JSON in string format</returns>
        private string RawJson(string apiRequest)
        {
            var rawJson = string.Empty;
            var request = (HttpWebRequest)WebRequest.Create(apiRequest);

            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                rawJson = reader.ReadToEnd();
            }
            return rawJson;
        }
        
    }
}
