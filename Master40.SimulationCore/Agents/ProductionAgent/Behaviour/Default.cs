﻿using Akka.Actor;
using Master40.DB.DataModel;
using Master40.DB.Enums;
using Master40.SimulationCore.Agents.DirectoryAgent;
using Master40.SimulationCore.Agents.HubAgent;
using Master40.SimulationCore.Helper;
using Master40.SimulationCore.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using Master40.SimulationCore.Agents.DispoAgent;
using Master40.SimulationCore.Agents.ProductionAgent.Types;
using static FArticles;
using static FAgentInformations;
using static FOperationResults;
using static FOperations;
using static FCreateSimulationWorks;
using static FArticleProviders;

namespace Master40.SimulationCore.Agents.ProductionAgent.Behaviour
{
    public class Default : SimulationCore.Types.Behaviour
    {
        internal Default(SimulationType simulationType = SimulationType.None)
            : base(childMaker: null, obj: simulationType)
        {
        }

        /// <summary>
        /// Operation related Hubagents
        /// </summary>
        internal AgentDictionary _hubAgents { get; set; } = new AgentDictionary();
        /// <summary>
        /// Article this Production Agent has to Produce
        /// </summary>
        internal FArticle _articleToProduce { get; set; }
        /// <summary>
        /// Class to supervise operations, supervise operation material handling, articles required by operation, and their relation
        /// </summary>
        internal OperationManager OperationManager { get; set; } = new OperationManager();
        
        internal ForwardScheduleTimeCalculator _forwardScheduleTimeCalculator { get; set; }
        public override bool Action(object message)
        {
            switch (message)
            {
                case Production.Instruction.StartProduction msg: StartProductionAgent(fArticle: msg.GetObjectFromMessage); break;
                case BasicInstruction.ResponseFromDirectory msg: SetHubAgent(hub: msg.GetObjectFromMessage); break;
                case BasicInstruction.JobForwardEnd msg: AddForwardTime(earliestStartForForwardScheduling: msg.GetObjectFromMessage); break;
                case BasicInstruction.ProvideArticle msg: ArticleProvided(msg.GetObjectFromMessage); break;
                case BasicInstruction.WithdrawRequiredArticles msg: WithdrawRequiredArticles(operationKey: msg.GetObjectFromMessage); break;
                // case Production.Instruction.FinishWorkItem fw: FinishWorkItem((Production)agent, fw.GetObjectFromMessage); break;
                // case Production.Instruction.ProvideRequest pr: ProvideRequest((Production)agent, pr.GetObjectFromMessage); break;
                // case Production.Instruction.Finished f:
                //     agent.VirtualChilds.Remove(agent.Sender);
                //     ((Production)agent).TryToFinish(); break;

                //Testing
                default: return true;
            }

            return true;
        }

        private void StartProductionAgent(FArticle fArticle)
        {
            _forwardScheduleTimeCalculator = new ForwardScheduleTimeCalculator(fArticle: fArticle);
            // check for Children
            if (fArticle.Article.ArticleBoms.Any())
            {
                Agent.DebugMessage(
                    msg: "Article: " + fArticle.Article.Name + " (" + fArticle.Key + ") is last leave in BOM.");
            }

            if (fArticle.Article.Operations == null)
                throw new Exception("Production agent without operations");
            

            // Ask the Directory Agent for Service
            RequestHubAgentsFromDirectoryFor(agent: Agent, operations: fArticle.Article.Operations);
            // And create Operations
            CreateJobsFromArticle(fArticle: fArticle);

            var requiredDispoAgents = OperationManager.CreateRequiredArticles(articleToProduce: fArticle
                , requestingAgent: Agent.Context.Self
                , currentTime: Agent.CurrentTime);

            for (var i = 0; i < requiredDispoAgents; i++)
            {
                // create Dispo Agents for to provide required articles
                var agentSetup = AgentSetup.Create(agent: Agent,
                    behaviour: DispoAgent.Behaviour.Factory.Get(simType: SimulationType.None));
                var instruction = Guardian.Instruction.CreateChild.Create(setup: agentSetup,
                    target: ((IAgent)Agent).Guardian, source: Agent.Context.Self);
                Agent.Send(instruction: instruction);
            }
        }

        private void SetHubAgent(FAgentInformation hub)
        {
            // Enqueue my Element at Hub Agent
            Agent.DebugMessage(msg: $"Received Agent from Directory: {Agent.Sender.Path.Name}");

            // add agent to current Scope.
            _hubAgents.Add(key: hub.Ref, value: hub.RequiredFor);
            // foreach fitting operation
            foreach (var operation in OperationManager.GetOperationBySkill(hub.RequiredFor))
            {
                operation.UpdateHubAgent(hub.Ref);
                Agent.Send(instruction: Hub.Instruction.EnqueueJob.Create(message: operation, target: hub.Ref));
            }

        }

        /// <summary>
        /// Return from Resource over Hub to ProductionAgent to initiate to withdraw the required articles
        /// </summary>
        /// <param name="operationKey"></param>
        private void WithdrawRequiredArticles(Guid operationKey)
        {
            var operation = OperationManager.GetOperationByKey(operationKey: operationKey);

            Agent.DebugMessage(msg: $"Withdraw required articles for operation: {operation.Operation.Name}");

            var dispoAgents = OperationManager.GetProviderForOperation(operationKey: operationKey); 
            

            foreach (var dispo in dispoAgents)
            {
                Agent.Send(Dispo.Instruction
                                .WithdrawArticleFromStock
                                .Create(message: "Production Start"
                                    , target: dispo));
            }
            
        }

        internal void Finished(Agent agent, FOperationResult operationResult)
        {

        }

        private void ProvideRequest(Production agent, Guid operationResult)
        {

        }

        /// <summary>
        /// set each material to provided and set the start condition true if all materials are provided
        /// </summary>
        /// <param name="fArticleProvider"></param>
        private void ArticleProvided(FArticleProvider fArticleProvider)
        {
            var articleDictionary = OperationManager.SetArticleProvided(fArticleProvider: fArticleProvider, providedBy: Agent.Sender, currentTime: Agent.CurrentTime);
            
            Agent.DebugMessage(msg: $"Article {fArticleProvider.ArticleName} {fArticleProvider.ArticleKey} for {_articleToProduce.Article.Name} {_articleToProduce.Key} has been provided");

            _articleToProduce.ProviderList.AddRange(fArticleProvider.Provider);
            
            if(articleDictionary.AllProvided())
            {
                Agent.DebugMessage(msg:$"All Article for {_articleToProduce.Article.Name} {_articleToProduce.Key} have been provided");

                articleDictionary.Operation.StartConditions.ArticlesProvided = true;
                
                Agent.Send(Hub.Instruction.SetOperationArticleProvided
                                          .Create(message: articleDictionary.Operation.Key
                                                 , target: articleDictionary.Operation.HubAgent));
            }

        }

        private void FinishOperation(Agent agent, FOperationResult operation)
        {

        }

        internal void RequestHubAgentsFromDirectoryFor(Agent agent, ICollection<M_Operation> operations)
        {
            // Request Hub Agent for Operations
            var resourceSkills = operations.Select(selector: x => x.ResourceSkill.Name).Distinct().ToList();
            foreach (var resourceSkillName in resourceSkills)
            {
                agent.Send(instruction: Directory.Instruction
                    .RequestAgent
                    .Create(discriminator: resourceSkillName
                        , target: agent.ActorPaths.HubDirectory.Ref));
            }
        }

        internal void CreateJobsFromArticle(FArticle fArticle)
        {
            var lastDue = fArticle.DueTime;
            var numberOfOperations = fArticle.Article.Operations.Count();
            var operationCounter = 0;
            foreach (var operation in fArticle.Article.Operations.OrderByDescending(keySelector: x => x.HierarchyNumber))
            {
                operationCounter++;
                var fJob = operation.ToOperationItem(dueTime: lastDue
                    , productionAgent: Agent.Context.Self
                    , firstOperation: (operationCounter == numberOfOperations)
                    , currentTime: Agent.CurrentTime);

                Agent.DebugMessage(
                    msg:
                    $"Created operation: {operation.Name} | BackwardStart {fJob.BackwardStart} | BackwardEnd:{fJob.BackwardEnd} Key: {fJob.Key}  ArticleKey: {fArticle.Key}");
                Agent.DebugMessage(
                    msg:
                    $"Precondition test: {operation.Name} | {fJob.StartConditions.PreCondition} ? {operationCounter} == {numberOfOperations} | Key: {fJob.Key}  ArticleKey: {fArticle.Key}");
                lastDue = fJob.BackwardStart - operation.AverageTransitionDuration;
                OperationManager.AddOperation(fJob);

                // send update to collector
                var pub = new FCreateSimulationWork(operation: fJob
                    , customerOrderId: fArticle.CustomerOrderId.ToString()
                    , isHeadDemand: fArticle.IsHeadDemand
                    , articleType: fArticle.Article.ArticleType.Name);
                Agent.Context.System.EventStream.Publish(@event: pub);
            }

            _articleToProduce = fArticle;
            SetForwardScheduling();
        }

        private void AddForwardTime(long earliestStartForForwardScheduling)
        {

            _forwardScheduleTimeCalculator.Add(earliestStartForForwardScheduling: earliestStartForForwardScheduling);
            SetForwardScheduling();
        }


        private void SetForwardScheduling()
        {
            Agent.DebugMessage(
                msg:
                $"AddForwardTime for {_articleToProduce.Article.Name} amount: {_forwardScheduleTimeCalculator.Count}  of {_forwardScheduleTimeCalculator.GetRequiredQuantity} ");

            if (!_forwardScheduleTimeCalculator.AllRequirementsFullFilled(fArticle: _articleToProduce))
                return;

            var operationList = new List<FOperation>();
            var earliestStart = Agent.CurrentTime;
            if (Agent.VirtualChildren.Count > 0)
                earliestStart = _forwardScheduleTimeCalculator.Max;

            foreach (var operation in OperationManager.GetOperations.OrderBy(keySelector: x => x.Operation.HierarchyNumber))
            {
                var newOperation = operation.SetForwardSchedule(earliestStart: earliestStart);
                earliestStart = newOperation.ForwardEnd + newOperation.Operation.AverageTransitionDuration;
                operationList.Add(item: newOperation);
            }

            Agent.DebugMessage(
                msg:
                $"EarliestForwardStart {earliestStart} for Article {_articleToProduce.Article.Name} ArticleKey: {_articleToProduce.Key} send to {Agent.VirtualParent} ");

            OperationManager.UpdateOperations(operations: operationList);
            Agent.Send(instruction: BasicInstruction.JobForwardEnd.Create(message: earliestStart,
                target: Agent.VirtualParent));
        }
    }
}
