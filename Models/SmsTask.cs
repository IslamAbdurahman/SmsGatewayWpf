using System;
using SmsGatewayApp.Helpers;

namespace SmsGatewayApp.Models
{
    public class SmsTask : ObservableObject
    {
        private int _id;
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        private string _title = string.Empty;
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private DateTime _createdAt;
        public DateTime CreatedAt { get => _createdAt; set => SetProperty(ref _createdAt, value); }

        private string _status = "Pending";
        public string Status { get => _status; set => SetProperty(ref _status, value); } // Pending, InProgress, Completed
    }
}
