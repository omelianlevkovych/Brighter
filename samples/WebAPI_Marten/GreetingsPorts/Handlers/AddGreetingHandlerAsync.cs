﻿using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.EntityGateway.Interfaces;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
    {
        private readonly IGreetingsEntityGateway unitOfWork;

        public AddGreetingHandlerAsync(IGreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<AddGreeting> HandleAsync(AddGreeting command, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                var person = await unitOfWork.GetPersonByName(command.Name);
                var greeting = new Greeting
                {
                    Message = command.Greeting,
                };

                person.AddGreeting(greeting);
                unitOfWork.UpdatePerson(person);

                await unitOfWork.CommitChanges();
            }

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
