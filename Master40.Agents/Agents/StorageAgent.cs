﻿using System;
using System.Linq;
using Master40.Agents.Agents.Internal;
using Master40.Agents.Agents.Model;
using Master40.DB.Enums;
using Master40.DB.Models;

namespace Master40.Agents.Agents
{
    public class StorageAgent : Agent
    {
        public Stock StockElement { get; set; }
        public StorageAgent(Agent creator, string name, bool debug, Stock stockElement) : base(creator, name, debug)
        {
            StockElement = stockElement;
            //Instructions = new List<Instruction>{ new Instruction{ Method = "RequestArticle", ExpectedObjecType = typeof(int) } };
        }

        public enum InstuctionsMethods
        {
            // Create and Return a Reservation for Article
            RequestArticle,
        }

        /// <summary>
        /// Returns the Reservation for Request
        /// ATTENTION: CAN BE - 0 -!
        /// </summary>
        /// <param name="instructionSet"></param>
        private void RequestArticle(InstructionSet instructionSet)
        {
            // debug
            DebugMessage(" requests Article " + StockElement.Name + " from Stock Agent ->" + instructionSet.SourceAgent.Name);

            // cast Request
            var request = instructionSet.ObjectToProcess as RequestItem;
            if (request == null)
                throw new InvalidCastException("Cast to Request Item Failed");

            // try to make Reservation
            var numberOfReservedItems =  TryToMakeReservationFor(request: request);
            
            // Create Callback
            CreateAndEnqueueInstuction(methodName: DispoAgent.InstuctionsMethods.ResponseFromStock.ToString(),
                                  objectToProcess: numberOfReservedItems, // may needs later a more complex answer for now just remove item from stock
                                      targetAgent: instructionSet.SourceAgent); // its Source Agent becaus this message is the Answer to the Instruction set.
                                     
        }

        /// <summary>
        /// Returns the Reservation Amont
        /// TODO: User more Complex Logic
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private int TryToMakeReservationFor(RequestItem request)
        {
            if ((StockElement.Current - StockElement.StockExchanges
                                                    .Where(x => x.RequiredOnTime <= Context.TimePeriod)
                                                    .Sum(x => x.Quantity) - request.Quantity) < 0)
            { 
                return 0;
            }

            StockElement.StockExchanges.Add(
            new StockExchange
                {
                    StockId = StockElement.Id,
                    EchangeType = EchangeType.Withdrawal,
                    Quantity = request.Quantity,
                    RequiredOnTime = request.DueTime,
                }
            );

            return request.Quantity;
        }

        
    }
}