# Food catalog

The catalog is the list of foods Moisson Montréal can receive. Each row is one kind of food. A donation of that food points at the row, and the price grid stores the price of one unit over time. Accounting matches the row through its Business Central code.

food_items is one donation: the donor, the quantity and the date. catalog_items is the food itself.

## Diagram

![Food catalog diagram](images/catalog.png)

The columns in this picture are the catalog item. The quality class column is a link to the quality_classes table: it stores the class code, such as A, B or C. The full database diagram in [schema.md](schema.md) shows that link together with the price grid.

## Tables

### catalog_items

One row per kind of food. The table was created for the price grid, and these columns complete it.

- **id**: code that identifies the catalog item, assigned automatically
- **name**: name of the food, for example Pain
- **category**: group the food belongs to, for example Boulangerie
- **business_central_code**: code of this food in Business Central. Two catalog items cannot share a code
- **unit**: unit used to count the food, such as kg or caisse. Free text for now, until issue #28 decides how the price of one unit works
- **quality_class**: quality class of this food. It must be a class that exists, such as A, B or C. The class is stored on the catalog item
- **created_at**: date and time the item was added, stored in UTC
- **updated_at**: date and time of the last change, stored in UTC. Empty until the item is edited

## Quality class

The quality class belongs to the catalog item, so every donation of that food can use the same class. The classes and their percentages live in the quality_classes table. A catalog item can only use a class that is already there.

A donation still keeps the quality class it was given, on food_items. That value is what a receipt uses.

## Business Central code

The code is how the catalog item is matched with the accounting system. The database rejects a second item with the same code. The comparison is exact, so `BC-PAIN` and `bc-pain` are two different codes.

## Rules enforced by the database

1. A name, a category, a Business Central code and a unit cannot be empty.
2. A Business Central code can belong to only one catalog item.
3. The quality class must be a quality class that exists.
4. A quality class cannot be deleted while a catalog item uses it.

## API

List, create and edit are available at `api/catalog-items`. Any signed-in employee can list the catalog. Creating or editing an item requires the ManagePriceGrid permission, so only an Admin can do it. Creating or editing an item with a Business Central code that is already used returns a conflict. A quality class that does not exist is refused.

## Import from Business Central

`POST api/catalog-items/import` receives an Excel export of Business Central (a .xlsx file in a form field named `file`, 10 MB at most) and creates or updates catalog items by their Business Central code. Like creating an item, it requires the ManagePriceGrid permission.

- The reader follows the purchase order (PO) export format, the only real Business Central export available so far: a `Lignes` sheet with the columns `Type`, `N°`, `Description` and `Code d'unité de mesure`. Columns are found by name, so their order does not matter. Only `Article` lines are imported. If the catalog later comes from another export, such as the Item list, the reader and this section must be updated.
- A new code creates an item. Since the export has no category or quality class, the item gets the category `To classify` and the quality class A, to be corrected by hand.
- A known code only updates the name. The category and the quality class are never changed by an import, so manual corrections are kept.
- The unit is never changed by an import either. Prices are per unit, so changing the unit of an item would silently change the meaning of all its prices. A line whose unit differs from the item's unit is reported, and the item is left unchanged. Units are compared without regard to upper or lower case, so kg and KG are the same.
- Importing the same file again creates no duplicates.
- A line that cannot be imported is listed in the report with its row number and the reason. The other lines are still imported.
- A file that cannot be read at all (not an .xlsx file, no `Lignes` sheet, a missing column, too large) is refused with a message that explains why.
- If another request adds an item with one of the file's codes at the same time, the import returns a conflict, like creating an item with a code already used. Nothing from the file is saved, and importing the file again works.

Code: [CatalogImportController.cs](../../src/MoissonMontreal.Api/Controllers/CatalogImportController.cs), [BusinessCentralExportReader.cs](../../src/MoissonMontreal.Api/Catalog/BusinessCentralExportReader.cs) and [CatalogImporter.cs](../../src/MoissonMontreal.Api/Catalog/CatalogImporter.cs).

## Migration

- Entity Framework migration: [20261117040905_AddCatalogItemDetails](../../src/MoissonMontreal.Api/Data/Migrations/20261117040905_AddCatalogItemDetails.cs)
- Equivalent SQL script: [0006_add_catalog_item_details.sql](sql/0006_add_catalog_item_details.sql)
- Table settings: [CatalogItemConfiguration.cs](../../src/MoissonMontreal.Api/Data/Configurations/CatalogItemConfiguration.cs)
- API: [CatalogItemsController.cs](../../src/MoissonMontreal.Api/Controllers/CatalogItemsController.cs)

Catalog items that existed before this migration get placeholder values, so the new rules can be added: category `To classify`, Business Central code `TO-FIX-` followed by the item id, unit `unit` and quality class A. Fix these items by hand after the migration.

The SQL script must be run after [0005_create_quality_classes.sql](sql/0005_create_quality_classes.sql). It records itself in the migration history, so Entity Framework will not apply it a second time.
