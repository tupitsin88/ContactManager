using Library.ContactResources;
using Spectre.Console;

namespace Library.Menu
{
    /// <summary>
    /// Статический класс, предоставляющий функционал для отображения главного меню.
    /// </summary>
    public static class Menu
    {
        /// <summary>
        /// Отображает главное меню и обрабатывает выбор пользователя.
        /// </summary>
        /// <param name="input">Входные данные для инициализации менеджера контактов.</param>
        /// <param name="filePath">Путь к файлу для сохранения данных при выходе.</param>
        /// <remarks>
        /// Метод отображает главное меню с различными опциями, такими как:
        /// - Показать контакты
        /// - Добавить контакт
        /// - Редактировать контакт
        /// - Удалить контакт
        /// - Поиск контактов
        /// - Отображение контактов в табличном виде
        /// - Добавить/Изменить дату рождения контакта
        /// - Показать ближайшие дни рождения
        /// - Показать Breakdown Chart по именам
        /// - Синхронизация с Yandex.Disk
        /// - Выход
        public static async Task ShowMainMenu(string input, string filePath)
        {
            ContactManager contactManager = new ContactManager();
            contactManager.ParseContacts(input);
            AnsiConsole.Clear();
            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan1]Главное меню:[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style(Color.Cyan1))
                        .AddChoices(
                        [
                        "Показать контакты",
                        "Добавить контакт",
                        "Редактировать контакт",
                        "Удалить контакт",
                        "Поиск контактов",
                        "Отображение контактов в табличном виде",
                        "Добавить/Изменить дату рождения контакта",
                        "Показать ближайшие дни рождения",
                        "Показать Breakdown Chart по именам",
                        "Синхронизация с Yandex.Disk",
                        "Выход"
                        ])
                );

                switch (choice)
                {
                    case "Показать контакты":
                        contactManager.ShowContacts();
                        break;
                    case "Добавить контакт":
                        contactManager.AddContact();
                        break;
                    case "Редактировать контакт":
                        contactManager.EditContact();
                        break;
                    case "Удалить контакт":
                        contactManager.RemoveContact();
                        break;
                    case "Поиск контактов":
                        contactManager.SearchContacts();
                        break;
                    case "Отображение контактов в табличном виде":
                        contactManager.FilterAndSortOptions();
                        break;
                    case "Добавить/Изменить дату рождения контакта":
                        contactManager.ChooseContactForUpdate();
                        break;
                    case "Показать ближайшие дни рождения":
                        contactManager.ShowUpcomingBirthdays();
                        break;
                    case "Показать Breakdown Chart по именам":
                        contactManager.CreateAndShowContactsNameBreakdown();
                        break;
                    case "Синхронизация с Yandex.Disk":
                        await contactManager.RunSyncMenu();
                        break;
                    case "Выход":
                        contactManager.WriteDataToFile(filePath);
                        break;
                }
            }
        }
    }
}
