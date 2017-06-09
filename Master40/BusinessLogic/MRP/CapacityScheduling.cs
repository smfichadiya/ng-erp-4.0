﻿using System.Collections.Generic;
using System.Linq;
using Master40.Models;
using Microsoft.EntityFrameworkCore;
using Master40.DB.Data.Context;
using Master40.DB.Models;
using System;

namespace Master40.BusinessLogic.MRP
{
    internal interface ICapacityScheduling
    {
        void GifflerThompsonScheduling();
        List<MachineGroupProductionOrderWorkSchedule> CapacityRequirementsPlanning();
        bool CapacityLevelingCheck(List<MachineGroupProductionOrderWorkSchedule> machineList);
        void SetMachines();
    }

    internal class CapacityScheduling : ICapacityScheduling
    {
        private readonly MasterDBContext _context;
        public List<LogMessage> Logger { get; set; }
        public CapacityScheduling(MasterDBContext context)
        {
            Logger = new List<LogMessage>();
            _context = context;
        }

        /// <summary>
        /// An algorithm for capacity-leveling. Writes Start/End in ProductionOrderWorkSchedule.
        /// </summary>
        public void GifflerThompsonScheduling()
        {
            var productionOrderWorkSchedules = GetProductionSchedules();
            ResetStartEnd(productionOrderWorkSchedules);
            productionOrderWorkSchedules = CalculateWorkTimeWithParents(productionOrderWorkSchedules);
            productionOrderWorkSchedules = CalculateNewDuration(productionOrderWorkSchedules);

            var plannableSchedules = new List<ProductionOrderWorkSchedule>();
            var plannedSchedules = new List<ProductionOrderWorkSchedule>();
            GetInitialPlannables(productionOrderWorkSchedules,plannedSchedules, plannableSchedules);

            while (plannableSchedules.Any())
            {
                //find next element by using the activity slack rule
                CalculateActivitySlack(plannableSchedules);
                var shortest = GetShortest(plannableSchedules);
                
                //build conflict set excluding the shortest process
                var conflictSet = GetConflictSet(shortest, productionOrderWorkSchedules, plannedSchedules);

                //set starttimes of conflicts after the shortest process
                SolveConflicts(shortest, conflictSet, productionOrderWorkSchedules);
                shortest.End = shortest.Start + shortest.Duration;

                plannedSchedules.Add(shortest);
                plannableSchedules.Remove(shortest);

                //search for parent and if available and allowed add it to the schedule
                var parent = GetParent(shortest, productionOrderWorkSchedules);
                if (parent != null && !plannableSchedules.Contains(parent) && IsTechnologicallyAllowed(parent,plannedSchedules)) plannableSchedules.Add(parent);
                _context.ProductionOrderWorkSchedule.Update(shortest);
                _context.SaveChanges();
            }
            SetMachines();
        }

        private List<ProductionOrderWorkSchedule> CalculateNewDuration(List<ProductionOrderWorkSchedule> productionOrderWorkSchedules)
        {
            //Capacity vs Duration vs Quantity
            //Todo: clone Pows for every machine
            //Todo: set pows machine
            //Todo: calculate duration for every pows/machine
            //Todo: which data do i need for gt alg?
            //Todo: perhaps calc with starttime 2,5 for 2 machines so that 1 is at 2 and the other at 3?
            //Todo: calc with duration of Quantity*duration/capacity

            return productionOrderWorkSchedules;
        }

        private void ResetStartEnd(List<ProductionOrderWorkSchedule> productionOrderWorkSchedules)
        {
            foreach (var productionOrderWorkSchedule in productionOrderWorkSchedules)
            {
                productionOrderWorkSchedule.Start = 0;
                productionOrderWorkSchedule.End = 0;
            }
        }

        /// <summary>
        /// Calculates Capacities needed to use backward/forward termination
        /// </summary>
        /// <returns>capacity-plan</returns>
        public List<MachineGroupProductionOrderWorkSchedule> CapacityRequirementsPlanning()
        {
            ClearMachineGroupProductionOrderWorkSchedule();
            //Stack for every hour and machinegroup
            var productionOrderWorkSchedules = GetProductionSchedules();
            var machineList = new List<MachineGroupProductionOrderWorkSchedule>();

            foreach (var productionOrderWorkSchedule in productionOrderWorkSchedules)
            {
                //calculate every pows for the amount of pieces ordered parallel
                for (var i= 0; i<productionOrderWorkSchedule.ProductionOrder.Quantity; i++)
                {
                    var machine = machineList.Find(a => a.MachineGroupId == productionOrderWorkSchedule.MachineGroupId);
                    if (machine != null)
                        AddToMachineGroup(machine, productionOrderWorkSchedule);
                    else
                    {
                        var schedule = new MachineGroupProductionOrderWorkSchedule()
                        {
                            MachineGroupId = productionOrderWorkSchedule.MachineGroupId,
                            ProductionOrderWorkSchedulesByTimeSteps = new List<ProductionOrderWorkSchedulesByTimeStep>()
                        };
                        machineList.Add(schedule);
                        _context.Add(schedule);
                        _context.SaveChanges();
                        AddToMachineGroup(machineList.Last(), productionOrderWorkSchedule);
                    }
                }
            }
            return machineList;
        }

        private void ClearMachineGroupProductionOrderWorkSchedule()
        {
            _context.MachineGroupProductionOrderWorkSchedules.RemoveRange(_context.MachineGroupProductionOrderWorkSchedules);
            _context.ProductionOrderWorkSchedulesByTimeSteps.RemoveRange(_context.ProductionOrderWorkSchedulesByTimeSteps);
            _context.SaveChanges();
        }

        /// <summary>
        /// checks if Capacity-leveling with Giffler-Thompson is necessary
        /// </summary>
        /// <param name="machineList"></param>
        /// <returns>true if existing plan exceeds capacity limits</returns>
        public bool CapacityLevelingCheck(List<MachineGroupProductionOrderWorkSchedule> machineList )
        {
            foreach (var machine in machineList)
            {
                foreach (var hour in machine.ProductionOrderWorkSchedulesByTimeSteps)
                {
                    var machines = _context.Machines.Where(a => a.MachineGroupId == machine.MachineGroupId);
                    if (!machines.Any()) continue;
                    if (machines.Count() < hour.ProductionOrderWorkSchedules.Count)
                        return true;
                }
            }
            
            return false;
        }

        private void AddToMachineGroup(MachineGroupProductionOrderWorkSchedule machine, ProductionOrderWorkSchedule productionOrderWorkSchedule)
        {
            var start = productionOrderWorkSchedule.StartBackward;
            var end = productionOrderWorkSchedule.EndBackward;
            if (productionOrderWorkSchedule.ProductionOrder.DemandProviderProductionOrders.First().State == State.ForwardScheduleExists)
            {
                start = productionOrderWorkSchedule.StartForward;
                end = productionOrderWorkSchedule.EndForward;
            }

            for (var i = start; i < end; i++)
            {
                var found = false;
                foreach (var productionOrderWorkSchedulesByTimeStep in machine.ProductionOrderWorkSchedulesByTimeSteps)
                {
                    if (productionOrderWorkSchedulesByTimeStep.Time == i)
                    {
                        productionOrderWorkSchedulesByTimeStep.ProductionOrderWorkSchedules.Add(productionOrderWorkSchedule);
                        found = true;
                        _context.Update(productionOrderWorkSchedulesByTimeStep);
                        _context.SaveChanges();
                        break;
                    }
                }
                if (!found)
                {
                    var timestep = new ProductionOrderWorkSchedulesByTimeStep()
                        {
                            Time = i,
                            ProductionOrderWorkSchedules = new List<ProductionOrderWorkSchedule>()
                            {
                                productionOrderWorkSchedule
                            }
                        };
                    machine.ProductionOrderWorkSchedulesByTimeSteps.Add(timestep);
                    _context.Add(timestep);
                    _context.SaveChanges();
                }
                    
            }
        }

       

        private bool IsTechnologicallyAllowed(ProductionOrderWorkSchedule schedule, List<ProductionOrderWorkSchedule> plannedSchedules)
        {
            var isAllowed = true;
            //check for every child if its planned
            foreach (var bom in schedule.ProductionOrder.ProductionOrderBoms)
            {
                if (bom.ProductionOrderChildId != schedule.ProductionOrderId)
                {
                    foreach (var childSchedule in bom.ProductionOrderChild.ProductionOrderWorkSchedule)
                    {
                        if (!plannedSchedules.Contains(childSchedule)) isAllowed = false;
                    }
                }
            }
            return isAllowed;
        }

        private List<ProductionOrderWorkSchedule> GetConflictSet(ProductionOrderWorkSchedule shortest, List<ProductionOrderWorkSchedule> productionOrderWorkSchedules, List<ProductionOrderWorkSchedule> plannedSchedules)
        {
            var conflictSet = new List<ProductionOrderWorkSchedule>();

            foreach (var schedule in productionOrderWorkSchedules)
            {
                if (schedule.MachineGroupId == shortest.MachineGroupId && !schedule.Equals(shortest) && !plannedSchedules.Contains(schedule))
                    conflictSet.Add(schedule);
            }
            var parent = GetParent(shortest, productionOrderWorkSchedules);
            if (parent != null)
                conflictSet.Add(parent);
            return conflictSet;
        }

        private void SolveConflicts(ProductionOrderWorkSchedule shortest, List<ProductionOrderWorkSchedule> conflictSet,
            List<ProductionOrderWorkSchedule> productionOrderWorkSchedules)
        {

            foreach (var conflict in conflictSet)
            {
                var index = productionOrderWorkSchedules.IndexOf(conflict);
                if (shortest.Start + shortest.Duration > productionOrderWorkSchedules[index].Start)
                {
                    productionOrderWorkSchedules[index].Start = shortest.Start + shortest.Duration + 1;
                }
            }
        }

        private ProductionOrderWorkSchedule GetShortest(List<ProductionOrderWorkSchedule> plannableSchedules)
        {
            ProductionOrderWorkSchedule shortest = null;
            foreach (var plannableSchedule in plannableSchedules)
            {
                if (shortest == null || shortest.ActivitySlack > plannableSchedule.ActivitySlack)
                    shortest = plannableSchedule;
            }
            return shortest;
        }

        private ProductionOrderWorkSchedule GetParent(ProductionOrderWorkSchedule schedule,List<ProductionOrderWorkSchedule> productionOrderWorkSchedules )
        {
            var parent = FindHierarchyParent(productionOrderWorkSchedules, schedule) ?? FindBomParent(schedule);
            return parent;
        }

        private List<ProductionOrderWorkSchedule> CalculateWorkTimeWithParents(List<ProductionOrderWorkSchedule> schedules)
        {
            foreach (var schedule in schedules)
            {
                schedule.WorkTimeWithParents = GetRemainTimeFromParents(schedule, schedules);
            }
            return schedules;
        }

        private void CalculateActivitySlack(List<ProductionOrderWorkSchedule> plannableSchedules)
        {
            foreach (var plannableSchedule in plannableSchedules)
            {
                //get duetime
                var demand = _context.Demands.Single(a => a.Id == plannableSchedule.ProductionOrder.DemandProviderProductionOrders.First().DemandRequester.DemandRequesterId);
                var dueTime = 9999;
                if (demand.GetType() == typeof(DemandOrderPart))
                {
                    dueTime = _context.OrderParts
                                .Include(a => a.Order)
                                .Single(a => a.Id == ((DemandOrderPart)demand).OrderPartId)
                                .Order
                                .DueTime;
                }
                

                //get remaining time
                plannableSchedule.ActivitySlack = dueTime - plannableSchedule.WorkTimeWithParents - plannableSchedule.Start;
                _context.ProductionOrderWorkSchedule.Update(plannableSchedule);
                
            }
            _context.SaveChanges();
        }

        private int GetRemainTimeFromParents(ProductionOrderWorkSchedule schedule, List <ProductionOrderWorkSchedule> productionOrderWorkSchedules)
        {
            var parent = GetParent(schedule,productionOrderWorkSchedules);
            if (parent == null) return schedule.Duration;
            
            return GetRemainTimeFromParents(parent, productionOrderWorkSchedules) + schedule.Duration;
        }

        private List<ProductionOrderWorkSchedule> GetProductionSchedules()
        {
            var demandRequester = _context.Demands
                                            .Include(a => a.DemandProvider)
                                            .Include(a => a.DemandRequester)
                                            .ThenInclude(a => a.DemandRequester)
                                                    .Where(b => b.State == State.BackwardScheduleExists 
                                                            || b.State == State.ExistsInCapacityPlan 
                                                            || b.State == State.ForwardScheduleExists)
                                                            .ToList();
           
            var productionOrderWorkSchedule = new List<ProductionOrderWorkSchedule>();
            foreach (var demandReq in demandRequester)
            {
                var schedules = GetProductionSchedules(demandReq);
                foreach (var schedule in schedules)
                {
                    productionOrderWorkSchedule.Add(schedule);
                }
            }
            return productionOrderWorkSchedule;
        }

        private List<ProductionOrderWorkSchedule> GetProductionSchedules(IDemandToProvider requester)
        {
            var provider =
                _context.Demands.OfType<DemandProviderProductionOrder>()
                    .Include(a => a.ProductionOrder)
                    .ThenInclude(c => c.ProductionOrderWorkSchedule)
                    .Include(b => b.ProductionOrder)
                    .ThenInclude(d => d.ProductionOrderBoms)
                    .Where(a => a.DemandRequester.DemandRequesterId == requester.Id)
                    .ToList();
            var schedules = new List<ProductionOrderWorkSchedule>();
            foreach (var prov in provider)
            {
                foreach (var schedule in prov.ProductionOrder.ProductionOrderWorkSchedule)
                {
                    schedules.Add(schedule);
                }
            }
            return schedules;
        }

        private void GetInitialPlannables(
            List<ProductionOrderWorkSchedule> productionOrderWorkSchedules, List<ProductionOrderWorkSchedule> plannedSchedules, List<ProductionOrderWorkSchedule> plannableSchedules)
        {
            foreach (var productionOrderWorkSchedule in productionOrderWorkSchedules)
            {
                var hasChildren = false;
                foreach (var bom in productionOrderWorkSchedule.ProductionOrder.ProductionOrderBoms)
                {
                    if (bom.ProductionOrderParent.Id == productionOrderWorkSchedule.ProductionOrderId)
                    {
                        hasChildren = true;
                        break;
                    }
                }
                if (hasChildren)
                    continue;
                //find out if its the lowest element in hierarchy
                var isLowestHierarchy = true;
                foreach (var mainSchedule in productionOrderWorkSchedules)
                {
                    if (mainSchedule.HierarchyNumber < productionOrderWorkSchedule.HierarchyNumber)
                        isLowestHierarchy = false;
                }
                if (isLowestHierarchy && !plannedSchedules.Contains(productionOrderWorkSchedule) && !plannableSchedules.Contains(productionOrderWorkSchedule))
                   plannableSchedules.Add(productionOrderWorkSchedule);

            }
        }

        private ProductionOrderWorkSchedule FindHierarchyParent(List<ProductionOrderWorkSchedule> productionOrderWorkSchedules, ProductionOrderWorkSchedule plannedSchedule  )
        {

            ProductionOrderWorkSchedule hierarchyParent = null;
            int hierarchyParentNumber = 100000;

            //find next higher element
            foreach (var mainSchedule in productionOrderWorkSchedules)
            {
                if (mainSchedule.ProductionOrderId == plannedSchedule.ProductionOrderId)
                {
                    if (mainSchedule.HierarchyNumber > plannedSchedule.HierarchyNumber &&
                        mainSchedule.HierarchyNumber < hierarchyParentNumber)
                    {
                        hierarchyParent = mainSchedule;
                        hierarchyParentNumber = mainSchedule.HierarchyNumber;
                    }
                }

            }
            return hierarchyParent;
        }

        private ProductionOrderWorkSchedule FindBomParent(ProductionOrderWorkSchedule plannedSchedule)
        {
            ProductionOrderWorkSchedule lowestHierarchyMember = null;
            foreach (var pob in _context.ProductionOrderBoms.Where(a => a.ProductionOrderChildId == plannedSchedule.ProductionOrderId))
            {
                //check if its the head element which points to itself
                if (pob.ProductionOrderParentId != plannedSchedule.ProductionOrder.Id)
                {
                    var parents = pob.ProductionOrderParent.ProductionOrderWorkSchedule;
                    lowestHierarchyMember = parents.First();
                    //find lowest hierarchy
                    foreach (var parent in parents)
                    {
                        if (parent.HierarchyNumber < lowestHierarchyMember.HierarchyNumber)
                            lowestHierarchyMember = parent;
                    }
                    break;
                }
            }
            return lowestHierarchyMember;
        }

        public void SetMachines()
        {
            //gets called when plan is fitting to capacities
            var schedules = GetProductionSchedules();
            foreach (var schedule in schedules)
            {
                var machines = _context.Machines.Where(a => a.MachineGroupId == schedule.MachineGroupId).ToList();
                if (!machines.Any()) continue;
                var schedulesOnMachineGroup = schedules.FindAll(a => a.MachineGroupId == schedule.MachineGroupId && a.MachineId != null);
                var crossingPows = new List<ProductionOrderWorkSchedule>();
                foreach (var scheduleMg in schedulesOnMachineGroup)
                {
                    if (detectCrossing(schedule, scheduleMg))
                        crossingPows.Add(schedule);
                }
                if (!crossingPows.Any()) schedule.MachineId = machines.First().Id;
                else
                {
                    for (var i = 0; i < machines.Count(); i++)
                    {
                        if (crossingPows.Find(a => a.MachineId == machines[i].Id) != null) continue;
                        schedule.MachineId = machines[i].Id;
                        break;
                    }
                }
            }
        }

        private bool detectCrossing(ProductionOrderWorkSchedule schedule, ProductionOrderWorkSchedule scheduleMg)
        {
            if ((scheduleMg.Start <= schedule.Start &&
                         scheduleMg.End > schedule.Start)
                        ||
                        (scheduleMg.Start < schedule.End &&
                        scheduleMg.End >= schedule.End)
                        ||
                        (scheduleMg.Start > schedule.Start &&
                         scheduleMg.End < schedule.End)
                        ||
                        (scheduleMg.Start <= schedule.Start &&
                         scheduleMg.End >= schedule.End))
            {
                return true;
            }
            return false;
        }
    }
}
