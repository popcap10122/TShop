﻿using eShopSolution.Data.EF;
using eShopSolution.Data.Entities;
using eShopSolution.ViewModels.Cart;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace eShopSolution.Application.Catolog.Orders
{
    public class OrderService : IOrderService
    {
        private readonly EShopDbContext _context;

        public OrderService(EShopDbContext context)
        {
            _context = context;
        }

        public async Task<int> Create(CheckoutRequest request)
        {
            var orderDetails = new List<OrderDetail>();

            foreach (var item in request.OrderDetails)
            {
                orderDetails.Add(new OrderDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }

            var order = new Order
            {
                ShipName = request.Name,
                ShipAddress = request.Address,
                ShipEmail = request.Email,
                ShipPhoneNumber = request.Phone,
                OrderDate = DateTime.Now,
                UserId = new Guid("95C83D7E-142E-401E-ED92-08D81E6BDE65"),
                Status = 0,
                OrderDetails = orderDetails
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return order.Id;
        }
    }
}