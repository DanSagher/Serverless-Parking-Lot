using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;
using System.Collections;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ParkingLot
{

    public class Function
    {

        /// <summary>
        /// A function that manage parking lot
        /// </summary>
        /// 
        public const string TABLE_NAME = "ParkingTable";

        #region public handlers
        public async Task<APIGatewayProxyResponse> AddEntryToParkingLot(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string plate = string.Empty;
            string parkingLot = string.Empty;
            var queryStrings = request?.QueryStringParameters;

            if (queryStrings == null || queryStrings.Count != 2)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Must enter plate and parking lot."
                };
            }

            foreach(KeyValuePair<string, string> dic in queryStrings)
            {
                if (dic.Key.Equals("plate"))
                {
                    plate = dic.Value;
                }

                if (dic.Key.Equals("parkingLot"))
                {
                    parkingLot = dic.Value;
                }
            }

            if (string.IsNullOrEmpty(plate) || string.IsNullOrEmpty(parkingLot))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Must enter plate and parking lot."
                };
            }
            else
            {
                string newId = await getAvailableId();
                AmazonDynamoDBClient client = new AmazonDynamoDBClient();

                var requestt = new PutItemRequest
                {
                    TableName = TABLE_NAME,
                    Item = new Dictionary<string, AttributeValue>()
                    {
                        { "TicketId", new AttributeValue { S = newId}},
                        { "Plate", new AttributeValue { S = plate }},
                        { "ParkingLot", new AttributeValue { S = parkingLot }},
                        { "Time", new AttributeValue { S = DateTime.Now.ToString() }}
                    }
                };

                await client.PutItemAsync(requestt);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = string.Format("Ticket ID: {0}. Please enter to the parking lot.", newId),
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                };

                return response;
            }
        }

        public async Task<APIGatewayProxyResponse> ExitParkinglot(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var queryStrings = request?.QueryStringParameters;
            string ticketId = string.Empty;
            AttributeValue val = null;

            if (queryStrings == null || queryStrings.Count != 1 || !queryStrings.TryGetValue("ticketId", out ticketId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Must enter ticket ID."
                };
            }

            ScanResponse currentList = await ScanParkingListAsync();
            foreach (Dictionary<string, AttributeValue> item in currentList.Items)
            {
                item.TryGetValue("TicketId", out val);
                if (val != null)
                {
                    if (val.S.Equals(ticketId))
                    {
                        string plate;
                        string parkedStartTime;
                        string parkedTime;
                        string parkingLotId;
                        string charge;
                        double parkedTimeInMinutes;

                        item.TryGetValue("Plate", out val);
                        plate = val.S;

                        item.TryGetValue("ParkingLot", out val);
                        parkingLotId = val.S;

                        item.TryGetValue("Time", out val);
                        parkedStartTime = val.S;

                        parkedTimeInMinutes = calcParkingTimeInMinutes(parkedStartTime);
                        parkedTime = string.Format("{0} hours and {1} minutes", Math.Floor(parkedTimeInMinutes / 60), String.Format("{0:0.00}", parkedTimeInMinutes % 60));
                        charge = (Math.Ceiling(parkedTimeInMinutes / (double)15) * 2.5).ToString() + "$";

                        string body = string.Format("License Plate: {0}, Total parked time: {1}, Parking lot id: {2}, Charge: {3}", plate, parkedTime, parkingLotId, charge);

                        var response = new APIGatewayProxyResponse
                        {
                            StatusCode = 200,
                            Body = body,
                            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                        };

                        await deleteTableEntry(ticketId, plate);

                        return response;
                    }
                }
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "Ticket not found."
            };
        }
        #endregion

        #region private methods

        private async Task deleteTableEntry(string ticketId, string plate)
        {
            AmazonDynamoDBClient client = new AmazonDynamoDBClient();
            Dictionary<string, AttributeValue> dic = new Dictionary<string, AttributeValue>();
            dic.Add("TicketId", new AttributeValue() { S = ticketId });
            dic.Add("Plate", new AttributeValue() { S = plate });

            await client.DeleteItemAsync(TABLE_NAME, dic);
        }

        private double calcParkingTimeInMinutes(string startTime)
        {
            DateTime start = DateTime.Parse(startTime);
            TimeSpan duration = DateTime.Now - start;
            return duration.TotalMinutes;
        }

        private async Task<ScanResponse> ScanParkingListAsync()
        {
            ScanResponse response;

            using (var client = new AmazonDynamoDBClient())
            {
                response = await client.ScanAsync(new ScanRequest(TABLE_NAME));
            }

            return (response);
        }

        private async Task<string> getAvailableId()
        {
            ScanResponse currentList = await ScanParkingListAsync();

            int index = 0;
            bool isFound = false;

            while (!isFound)
            {
                if (isTicketIdAvailable(currentList, index))
                {
                    isFound = true;
                }
                else
                {
                    index++;
                }
            }

            return index.ToString();
        }

        private bool isTicketIdAvailable(ScanResponse response, int id)
        {
            AttributeValue val = null;

            foreach (Dictionary<string, AttributeValue> item in response.Items)
            {
                item.TryGetValue("TicketId", out val);
                if (val != null)
                {
                    if (val.S.Equals(id.ToString()))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        #endregion
    }
}
