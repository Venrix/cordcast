class AudioDevice {
  final String id;
  final String name;

  const AudioDevice({required this.id, required this.name});

  factory AudioDevice.fromJson(Map<String, dynamic> json) => AudioDevice(
        id: json['id'] as String,
        name: json['name'] as String,
      );

  Map<String, dynamic> toJson() => {'id': id, 'name': name};

  @override
  bool operator ==(Object other) => other is AudioDevice && other.id == id;

  @override
  int get hashCode => id.hashCode;

  @override
  String toString() => name;
}
