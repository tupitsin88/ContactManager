using Spectre.Console;

namespace Library.ContactResources
{
    /// <summary>
    /// Представляет объект контакта с уникальным номером, именем, фамилией, номером телефона,
    /// датой рождения и почтой.
    /// </summary>
    public class Contact
    {
        /// <summary>
        /// Уникальный ID контакта.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Имя контакта.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Фамилия контакта.
        /// </summary>
        public string? SecondName { get; set; }

        /// <summary>
        /// Дата рождения контакта.
        /// </summary>
        public string? DateOfBirth { get; set; } = "Неизвестно";

        /// <summary>
        /// Номер телефона контакта (формат: +7XXXXXXXXXX).
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Email контакта.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Конструктор без параметров.
        /// </summary>
        public Contact() { }

        /// <summary>
        /// Конструктор с параметрами.
        /// </summary>
        /// <param name="id">Уникальный ID.</param>
        /// <param name="firstName">Имя.</param>
        /// <param name="secondName">Фамилия.</param>
        /// <param name="phone">Номер телефона.</param>
        /// <param name="email">Почта.</param>
        public Contact(int id, string firstName, string secondName, string phone, string email)
        {
            Id = id;
            FirstName = firstName;
            SecondName = secondName;
            Phone = phone;
            Email = email;
        }

        /// <summary>
        /// Устанавливает дату рождения контакта.
        /// </summary>
        /// <param name="dateOfBirth">Дата рождения в формате dd.mm.</param>
        public void SetDateOfBirth(string dateOfBirth)
        {
            if (string.IsNullOrWhiteSpace(dateOfBirth))
            {
                DateOfBirth = "Неизвестно";
                return;
            }

            // Проверяем формат даты
            if (IsValidDate(dateOfBirth))
            {
                DateOfBirth = dateOfBirth;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Некорректный формат даты. Используйте dd.mm (например, 15.06).[/]");
            }
        }

        /// <summary>
        /// Проверяет корректность формата даты.
        /// </summary>
        /// <param name="date">Дата для проверки.</param>
        /// <returns>True, если формат корректен, иначе False.</returns>
        private static bool IsValidDate(string date)
        {
            // Регулярное выражение для проверки формата dd.mm
            string regex = @"^(0[1-9]|[12][0-9]|3[01])\.(0[1-9]|1[0-2])$";
            return System.Text.RegularExpressions.Regex.IsMatch(date, regex);
        }
    }
}
