import csv

def merge_csv_files(a_file, b_file, output_file):
    # Dictionary to store datatypes for each name from B.csv
    name_datatypes = {}
    
    # Read B.csv (semicolon-separated)
    with open(b_file, 'r', newline='') as b_csv:
        b_reader = csv.reader(b_csv, delimiter=';')
        # Skip header if it exists
        next(b_reader, None)
        
        # Process each row in B.csv
        for row in b_reader:
            if len(row) >= 3:  # Ensure row has at least 3 columns
                app_name, name, datatype = row[0], row[1], row[2]
                # Add datatype to set for this name
                if name in name_datatypes:
                    name_datatypes[name].add(datatype)
                else:
                    name_datatypes[name] = {datatype}
    
    # Read A.csv and write to output
    with open(a_file, 'r', newline='') as a_csv, \
         open(output_file, 'w', newline='') as out_csv:
        
        a_reader = csv.reader(a_csv)
        writer = csv.writer(out_csv)
        
        # Read header from A.csv
        header = next(a_reader)
        # Add new 'datatypes' column to header
        header.append('datatypes')
        writer.writerow(header)
        
        # Process each row in A.csv
        for row in a_reader:
            if row:  # Check if row is not empty
                name = row[0]  # First column is name
                # Get datatypes for this name if they exist
                datatypes = ','.join(name_datatypes.get(name, set()))
                # Append datatypes to the row
                row.append(datatypes)
                writer.writerow(row)

# Example usage
try:
    merge_csv_files('ab_blender/last_data.csv', 'ethip-endpoint.csv', 'output.csv')
    print("Files merged successfully into output.csv")
except FileNotFoundError as e:
    print(f"Error: One of the input files was not found: {e}")
except Exception as e:
    print(f"An error occurred: {e}")