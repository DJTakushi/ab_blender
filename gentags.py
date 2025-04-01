import csv
import json

def csv_to_json(input_csv_file, output_json_file):
    # List to store all entries
    result = []
    
    try:
        # Open and read the CSV file
        with open(input_csv_file, 'r') as csv_file:
            # Create CSV reader object
            csv_reader = csv.reader(csv_file, delimiter=';')
            
            # Process each row
            for row in csv_reader:
                # Check if row has at least 3 elements
                if len(row) >= 3:
                    # Create dictionary for each entry
                    entry = {
                        "app_name": row[0],
                        "Name": row[1],
                        "DataType": row[2],
                        "Path": "1,0"
                    }
                    result.append(entry)
                
        # Write to JSON file
        with open(output_json_file, 'w') as json_file:
            json.dump(result, json_file, indent=4)
            
        print(f"Successfully converted {input_csv_file} to {output_json_file}")
        
    except FileNotFoundError:
        print(f"Error: Input file '{input_csv_file}' not found")
    except Exception as e:
        print(f"An error occurred: {str(e)}")

# Example usage
if __name__ == "__main__":
    # You can change these file names as needed
    input_file = "ethip-endpoint.csv"
    output_file = "plc_blender/tags.json"
    
    csv_to_json(input_file, output_file)