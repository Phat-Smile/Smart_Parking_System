pipeline {
    agent { label 'parkwise-dotnet10' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def checkoutInfo = checkout scm
                    env.CHECKED_OUT_BRANCH = checkoutInfo.GIT_BRANCH ?: ''
                }
            }
        }

        stage('Verify develop branch') {
            steps {
                script {
                    def allowedBranches = ['develop', 'origin/develop', 'refs/heads/develop', 'refs/remotes/origin/develop']
                    if (!allowedBranches.contains(env.CHECKED_OUT_BRANCH)) {
                        error "Expected develop checkout, got '${env.CHECKED_OUT_BRANCH ?: 'unknown'}'"
                    }
                    if (env.BRANCH_NAME && env.BRANCH_NAME != 'develop') {
                        error "Expected develop job, got '${env.BRANCH_NAME}'"
                    }
                }
            }
        }

        stage('Restore') {
            steps {
                sh 'dotnet --version'
                sh 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers'
            }
        }

        stage('Unit Tests') {
            steps {
                sh '''
                    set -e
                    dotnet test tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Booking/Booking.UnitTests/Booking.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Payment/Payment.UnitTests/Payment.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Notification/Notification.UnitTests/Notification.UnitTests.csproj --no-build --no-restore
                '''
            }
        }

        stage('Architecture Tests') {
            steps {
                sh 'dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore'
            }
        }

        stage('Web Foundation Tests') {
            steps {
                sh 'dotnet test tests/WebFoundationTests/WebFoundationTests.csproj --no-build --no-restore'
            }
        }
    }
}
